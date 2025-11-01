using System.Data;
using System.Linq;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Infra.Services;

public class MarketService : IMarketService
{
    private const int SquadLimit = 23;

    private readonly DraftDbContext _dbContext;
    private readonly IMarketCycleGenerator _cycleGenerator;
    private readonly MarketOptions _marketOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ITransactionLogService _transactionLogService;

    public MarketService(DraftDbContext dbContext, IMarketCycleGenerator cycleGenerator, IOptions<MarketOptions> options, ITransactionLogService transactionLogService, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cycleGenerator = cycleGenerator ?? throw new ArgumentNullException(nameof(cycleGenerator));
        _marketOptions = options.Value ?? throw new ArgumentNullException(nameof(options));
        _transactionLogService = transactionLogService ?? throw new ArgumentNullException(nameof(transactionLogService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MarketCycleDto> EnsureCycleAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (await _cycleGenerator.NeedsNewCycleAsync(now, ct).ConfigureAwait(false))
        {
            return await _cycleGenerator.CreateNewCycleAsync(now, ct).ConfigureAwait(false);
        }

        var activeCycle = await _dbContext.MarketCycles
            .AsNoTracking()
            .Where(c => c.Status == MarketCycleStatus.Active)
            .OrderByDescending(c => c.StartsAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (activeCycle is not null)
        {
            return ToCycleDto(activeCycle);
        }

        var upcomingDraft = await _dbContext.MarketCycles
            .AsNoTracking()
            .Where(c => c.Status == MarketCycleStatus.Draft && c.StartsAtUtc > now)
            .OrderBy(c => c.StartsAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (upcomingDraft is not null)
        {
            return ToCycleDto(upcomingDraft);
        }

        var last = await _dbContext.MarketCycles
            .AsNoTracking()
            .OrderByDescending(c => c.StartsAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (last is not null)
        {
            return ToCycleDto(last);
        }

        var fallbackStarts = now;
        var fallbackEnds = now.AddHours(_marketOptions.CycleDurationHours);

        return new MarketCycleDto(
            Guid.Empty,
            "Ciclo de Mercado (pré-visualização)",
            MarketCycleStatus.Draft,
            fallbackStarts,
            fallbackEnds,
            fallbackStarts,
            fallbackStarts,
            null);
    }

    public async Task<List<MarketItemDto>> GetActiveItemsAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var items = await _dbContext.MarketItems
            .AsNoTracking()
            .Where(i => i.Status == MarketItemStatus.Active && i.ExpiresAtUtc > now)
            .Include(i => i.Player)
                .ThenInclude(p => p.Position)
            .Include(i => i.CurrentLeaderTeam)
            .OrderBy(i => i.ExpiresAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return items.Select(ToDto).ToList();
    }

    public async Task<MarketItemDto?> GetItemAsync(Guid itemId, CancellationToken ct)
    {
        var item = await _dbContext.MarketItems
            .AsNoTracking()
            .Include(i => i.Player)
                .ThenInclude(p => p.Position)
            .Include(i => i.CurrentLeaderTeam)
            .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
            .ConfigureAwait(false);

        return item is null ? null : ToDto(item);
    }

    public async Task<BidResultDto> PlaceBidAsync(Guid itemId, string teamToken, decimal amount, uint expectedRowVersion, CancellationToken ct)
    {
        if (amount <= 0)
            throw new MarketValidationException("O valor do lance deve ser maior que zero.");

        EnsureExpectedRowVersion(expectedRowVersion);

        var normalizedToken = NormalizeToken(teamToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        return await InSerializableTxAsync<BidResultDto>(async ct2 =>
        {
            var item = await _dbContext.MarketItems
                .Include(i => i.Player).ThenInclude(p => p.Position)
                .FirstOrDefaultAsync(i => i.ItemId == itemId, ct2)
                ?? throw new MarketNotFoundException("Item de mercado não encontrado.");

            EnsureRowVersion(item.RowVersion, expectedRowVersion);

            if (item.Status != MarketItemStatus.Active)
                throw new MarketConflictException("O item não está disponível para lances.");

            if (item.ExpiresAtUtc <= now)
                throw new MarketConflictException("O item já expirou. Atualize a página e tente novamente.");

            var team = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.Token == normalizedToken, ct2)
                ?? throw new MarketForbiddenException("Token de time inválido.");

            var minimumBid = item.CurrentLeaderAmount.HasValue
                ? item.CurrentLeaderAmount.Value + item.MinIncrement
                : item.BasePrice;

            if (amount < minimumBid)
                throw new MarketValidationException($"O lance mínimo permitido é {minimumBid:0.00}.");

            await EnsureSquadLimitAsync(team.TeamId, item.ItemId, item.CurrentLeaderTeamId, ct2);

            var availableBudget = team.Budget - team.BudgetBlocked;
            if (availableBudget < amount)
                throw new MarketConflictException("Saldo insuficiente para registrar o lance.");

            var previousLeaderId = item.CurrentLeaderTeamId;
            var previousAmount = item.CurrentLeaderAmount ?? 0m;
            string? outbidNotes = null;

            if (previousLeaderId.HasValue)
            {
                var previousTeam = await _dbContext.Teams
                    .FirstOrDefaultAsync(t => t.TeamId == previousLeaderId.Value, ct2);

                if (previousTeam is not null)
                {
                    previousTeam.BudgetBlocked = Math.Max(0m, previousTeam.BudgetBlocked - previousAmount);
                    var previousTeamName = string.IsNullOrWhiteSpace(previousTeam.TeamName)
                        ? previousTeam.TeamId.ToString()
                        : previousTeam.TeamName;
                    outbidNotes = $"Time {previousTeamName} foi superado no leilão.";
                }
                else
                {
                    outbidNotes = "Líder anterior não encontrado ao liberar orçamento.";
                }
            }

            team.BudgetBlocked += amount;

            var bid = new MarketBid
            {
                BidId = Guid.NewGuid(),
                ItemId = item.ItemId,
                TeamId = team.TeamId,
                Amount = amount,
                CreatedAtUtc = now
            };

            item.CurrentLeaderTeamId = team.TeamId;
            item.CurrentLeaderAmount = amount;
            item.LastUpdateUtc = now;

            var bidNotes = FormattableString.Invariant($"Lance de {amount:0.00} registrado.");
            if (!string.IsNullOrWhiteSpace(outbidNotes))
                bidNotes = $"{bidNotes} {outbidNotes}";

            await _transactionLogService.LogMarketAsync(
                item, MarketTransactionType.BidPlaced, team.TeamId, previousLeaderId,
                amount, team.TeamId.ToString(), bidNotes, now, ct2);

            await _dbContext.MarketBids.AddAsync(bid, ct2);

            try
            {
                await _dbContext.SaveChangesAsync(ct2);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new MarketPreconditionFailedException(
                    "O item foi atualizado por outro time. Atualize a página e tente novamente.", ex);
            }

            return new BidResultDto(true, "Lance registrado com sucesso.", amount);
        }, ct);
    }

    public async Task<BuyNowResultDto> BuyNowAsync(Guid itemId, string teamToken, uint expectedRowVersion, CancellationToken ct)
    {
        EnsureExpectedRowVersion(expectedRowVersion);

        var normalizedToken = NormalizeToken(teamToken);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        return await InSerializableTxAsync<BuyNowResultDto>(async ct2 =>
        {
            var item = await _dbContext.MarketItems
                .Include(i => i.Player).ThenInclude(p => p.Position)
                .FirstOrDefaultAsync(i => i.ItemId == itemId, ct2)
                ?? throw new MarketNotFoundException("Item de mercado não encontrado.");

            EnsureRowVersion(item.RowVersion, expectedRowVersion);

            var previousLeaderId = item.CurrentLeaderTeamId;

            if (item.Status != MarketItemStatus.Active)
                throw new MarketConflictException("O item não está disponível para compra imediata.");

            if (!item.BuyNowPrice.HasValue)
                throw new MarketConflictException("O item não possui opção de compra imediata.");

            var buyNowPrice = item.BuyNowPrice.Value;

            if (item.ExpiresAtUtc <= now)
                throw new MarketConflictException("O item já expirou. Atualize e tente novamente.");

            var team = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.Token == normalizedToken, ct2)
                ?? throw new MarketForbiddenException("Token de time inválido.");

            await EnsureSquadLimitAsync(team.TeamId, item.ItemId, null, ct2, includeCurrentItem: true);

            var blockedForItem = item.CurrentLeaderTeamId == team.TeamId ? item.CurrentLeaderAmount ?? 0m : 0m;
            var available = team.Budget - team.BudgetBlocked + blockedForItem;

            if (available < buyNowPrice)
                throw new MarketConflictException("Saldo insuficiente para comprar agora.");

            var player = await _dbContext.Players
                .FirstOrDefaultAsync(p => p.PlayerId == item.PlayerId, ct2)
                ?? throw new MarketNotFoundException("Jogador não encontrado para este item.");

            if (player.CurrentTeamId.HasValue && player.CurrentTeamId != team.TeamId)
                throw new MarketConflictException("O jogador já está vinculado a outro elenco.");

            Team? previousLeaderTeam = null;

            if (item.CurrentLeaderTeamId.HasValue)
            {
                var previousTeam = await _dbContext.Teams
                    .FirstOrDefaultAsync(t => t.TeamId == item.CurrentLeaderTeamId.Value, ct2);

                if (previousTeam is not null)
                {
                    previousTeam.BudgetBlocked = Math.Max(0m, previousTeam.BudgetBlocked - (item.CurrentLeaderAmount ?? 0m));
                    previousLeaderTeam = previousTeam;
                }
            }

            string? takeoverNotes = null;
            if (previousLeaderId.HasValue && previousLeaderId != team.TeamId)
            {
                takeoverNotes = previousLeaderTeam is not null
                    ? $"Time {(!string.IsNullOrWhiteSpace(previousLeaderTeam.TeamName) ? previousLeaderTeam.TeamName : previousLeaderTeam.TeamId.ToString())} foi superado na compra imediata."
                    : "Líder anterior não encontrado ao processar compra imediata.";
            }

            team.Budget -= buyNowPrice;
            team.BudgetBlocked = Math.Max(0m, team.BudgetBlocked - blockedForItem);

            player.CurrentTeamId = team.TeamId;
            await SyncRosterAsync(team.TeamId, player.PlayerId, ct2);

            item.Status = MarketItemStatus.Sold;
            item.WinnerTeamId = team.TeamId;
            item.LastUpdateUtc = now;
            item.ExpiresAtUtc = now;

            var buyNowNotes = FormattableString.Invariant($"Compra imediata por {buyNowPrice:0.00}.");
            if (!string.IsNullOrWhiteSpace(takeoverNotes))
                buyNowNotes = $"{buyNowNotes} {takeoverNotes}";

            await _transactionLogService.LogMarketAsync(
                item, MarketTransactionType.BuyNow, team.TeamId, previousLeaderId,
                buyNowPrice, team.TeamId.ToString(), buyNowNotes, now, ct2);

            await _dbContext.TransferHistories.AddAsync(new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                PlayerId = player.PlayerId,
                FromTeamId = null,
                ToTeamId = team.TeamId,
                Amount = buyNowPrice,
                Type = TransferType.MarketAuction,
                Notes = "Compra imediata",
                PerformedBy = "sistema",
                PerformedAtUtc = now
            }, ct2);

            try
            {
                await _dbContext.SaveChangesAsync(ct2);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new MarketPreconditionFailedException(
                    "O item foi atualizado por outro time. Atualize a página e tente novamente.", ex);
            }

            return new BuyNowResultDto(true, "Compra realizada com sucesso.");
        }, ct);
    }

    public async Task<int> CloseExpiredItemsAsync(CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var itemIds = await _dbContext.MarketItems
            .AsNoTracking()
            .Where(i => i.Status == MarketItemStatus.Active && i.ExpiresAtUtc <= now)
            .Select(i => i.ItemId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (itemIds.Count == 0)
        {
            return 0;
        }

        var processed = 0;

        foreach (var itemId in itemIds)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

            var item = await _dbContext.MarketItems
                .Include(i => i.Player)
                .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
                .ConfigureAwait(false);

            if (item is null)
            {
                continue;
            }

            if (item.Status != MarketItemStatus.Active)
            {
                continue;
            }

            if (item.ExpiresAtUtc > now)
            {
                continue;
            }

            var previousLeaderId = item.CurrentLeaderTeamId;
            var previousLeaderAmount = item.CurrentLeaderAmount;

            if (item.CurrentLeaderTeamId.HasValue && item.CurrentLeaderAmount.HasValue)
            {
                var team = await _dbContext.Teams
                    .FirstOrDefaultAsync(t => t.TeamId == item.CurrentLeaderTeamId.Value, ct)
                    .ConfigureAwait(false);

                if (team is null)
                {
                    item.Status = MarketItemStatus.Expired;
                    item.CurrentLeaderTeamId = null;
                    item.CurrentLeaderAmount = null;
                    item.WinnerTeamId = null;

                    await _transactionLogService.LogMarketAsync(
                        item,
                        MarketTransactionType.AuctionExpired,
                        null,
                        previousLeaderId,
                        previousLeaderAmount,
                        "sistema",
                        "Leilão encerrado sem vencedor válido.",
                        now,
                        ct).ConfigureAwait(false);
                }
                else
                {
                    await EnsureSquadLimitAsync(team.TeamId, item.ItemId, item.CurrentLeaderTeamId, ct, includeCurrentItem: true).ConfigureAwait(false);

                    var value = item.CurrentLeaderAmount.Value;
                    team.BudgetBlocked = Math.Max(0m, team.BudgetBlocked - value);
                    team.Budget -= value;
                    if (team.Budget < 0m)
                    {
                        team.Budget = 0m;
                    }

                    var player = await _dbContext.Players
                        .FirstOrDefaultAsync(p => p.PlayerId == item.PlayerId, ct)
                        .ConfigureAwait(false);

                    if (player is not null)
                    {
                        player.CurrentTeamId = team.TeamId;
                        await SyncRosterAsync(team.TeamId, player.PlayerId, ct).ConfigureAwait(false);
                    }

                    item.Status = MarketItemStatus.Sold;
                    item.WinnerTeamId = team.TeamId;

                    await _dbContext.TransferHistories.AddAsync(new TransferHistory
                    {
                        TransferId = Guid.NewGuid(),
                        PlayerId = item.PlayerId,
                        FromTeamId = null,
                        ToTeamId = team.TeamId,
                        Amount = value,
                        Type = TransferType.MarketAuction,
                        Notes = "Leilão encerrado",
                        PerformedBy = "sistema",
                        PerformedAtUtc = now
                    }, ct).ConfigureAwait(false);

                    var settleNotes = FormattableString.Invariant($"Leilão encerrado por {value:0.00}.");

                    await _transactionLogService.LogMarketAsync(
                        item,
                        MarketTransactionType.AuctionSettled,
                        team.TeamId,
                        previousLeaderId,
                        value,
                        "sistema",
                        settleNotes,
                        now,
                        ct).ConfigureAwait(false);
                }
            }
            else
            {
                item.Status = MarketItemStatus.Expired;
                item.CurrentLeaderTeamId = null;
                item.CurrentLeaderAmount = null;
                item.WinnerTeamId = null;

                await _transactionLogService.LogMarketAsync(
                    item,
                    MarketTransactionType.AuctionExpired,
                    null,
                    null,
                    null,
                    "sistema",
                    "Leilão expirado sem lances válidos.",
                    now,
                    ct).ConfigureAwait(false);
            }

            item.LastUpdateUtc = now;
            item.ExpiresAtUtc = now;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
            processed++;
        }

        await UpdateCycleStatusesAsync(now, ct).ConfigureAwait(false);

        return processed;
    }

    private async Task UpdateCycleStatusesAsync(DateTime now, CancellationToken ct)
    {
        var cycleIds = await _dbContext.MarketItems
            .AsNoTracking()
            .Select(i => i.CycleId)
            .Distinct()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var cycleId in cycleIds)
        {
            var hasActive = await _dbContext.MarketItems
                .AsNoTracking()
                .AnyAsync(i => i.CycleId == cycleId && i.Status == MarketItemStatus.Active, ct)
                .ConfigureAwait(false);

            if (!hasActive)
            {
                var cycle = await _dbContext.MarketCycles
                    .FirstOrDefaultAsync(c => c.CycleId == cycleId, ct)
                    .ConfigureAwait(false);

                if (cycle is not null && cycle.Status == MarketCycleStatus.Active)
                {
                    cycle.Status = MarketCycleStatus.Closed;
                    cycle.UpdatedAtUtc = now;
                    _dbContext.MarketCycles.Update(cycle);
                }
            }
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task EnsureSquadLimitAsync(Guid teamId, Guid itemId, Guid? currentLeaderTeamId, CancellationToken ct, bool includeCurrentItem = false)
    {
        var currentPlayers = await _dbContext.Players
            .AsNoTracking()
            .CountAsync(p => p.CurrentTeamId == teamId, ct)
            .ConfigureAwait(false);

        var activeLeads = await _dbContext.MarketItems
            .AsNoTracking()
            .CountAsync(i => i.Status == MarketItemStatus.Active && i.CurrentLeaderTeamId == teamId && i.ItemId != itemId, ct)
            .ConfigureAwait(false);

        var projected = currentPlayers + activeLeads;

        if (includeCurrentItem || currentLeaderTeamId != teamId)
        {
            projected += 1;
        }

        if (projected > SquadLimit)
        {
            throw new MarketValidationException("O time atingiria o limite de 23 jogadores.");
        }
    }

    private async Task SyncRosterAsync(Guid teamId, int playerId, CancellationToken ct)
    {
        var existingEntries = await _dbContext.TeamRosters
            .Where(r => r.PlayerId == playerId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var entry in existingEntries)
        {
            if (entry.TeamId != teamId)
            {
                _dbContext.TeamRosters.Remove(entry);
            }
        }

        if (!existingEntries.Any(e => e.TeamId == teamId))
        {
            await _dbContext.TeamRosters.AddAsync(new TeamRoster
            {
                PlayerId = playerId,
                TeamId = teamId
            }, ct).ConfigureAwait(false);
        }
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new MarketForbiddenException("Token de time é obrigatório.");
        }

        return token.Trim();
    }

    private static MarketItemDto ToDto(MarketItem item)
    {
        var statusText = item.Status.ToDisplayName();

        var positionName = item.Player.Position?.Name ?? string.Empty;
        var age = item.Player.Age ?? 0;

        return new MarketItemDto(
            item.ItemId,
            item.CycleId,
            item.PlayerId,
            item.Player.Name,
            positionName,
            item.Player.Overall,
            age,
            item.BasePrice,
            item.BuyNowPrice,
            item.MinIncrement,
            item.ExpiresAtUtc,
            statusText,
            item.CurrentLeaderAmount,
            item.CurrentLeaderTeam?.TeamName,
            item.CurrentLeaderTeamId,
            item.RowVersion);
    }

    private static MarketCycleDto ToCycleDto(MarketCycle cycle)
    {
        return new MarketCycleDto(
            cycle.CycleId,
            cycle.Name,
            cycle.Status,
            cycle.StartsAtUtc,
            cycle.EndsAtUtc,
            cycle.CreatedAtUtc,
            cycle.UpdatedAtUtc,
            cycle.Notes);
    }

    private static void EnsureExpectedRowVersion(uint expectedRowVersion)
    {
        if (expectedRowVersion == 0)
        {
            throw new MarketPreconditionFailedException("A versão informada do item é inválida.");
        }
    }

    private static void EnsureRowVersion(uint current, uint expected)
    {
        if (current != expected)
        {
            throw new MarketPreconditionFailedException("O item foi atualizado por outro time. Atualize a página e tente novamente.");
        }
    }

    private async Task<T> InSerializableTxAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var result = await work(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }
}
