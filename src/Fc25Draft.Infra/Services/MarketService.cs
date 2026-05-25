using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Options;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Fc25Draft.Infra.Services;

public class MarketService : IMarketService
{
    private const int SquadLimit = 23;

    private readonly DraftDbContext _dbContext;
    private readonly IMarketCycleGenerator _cycleGenerator;
    private readonly MarketOptions _marketOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ITransactionLogService _transactionLogService;
    private readonly IBudgetService _budgetService;


    public MarketService(
        DraftDbContext dbContext,
        IMarketCycleGenerator cycleGenerator,
        IOptions<MarketOptions> options,
        ITransactionLogService transactionLogService,
        IBudgetService budgetService,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cycleGenerator = cycleGenerator ?? throw new ArgumentNullException(nameof(cycleGenerator));
        _marketOptions = options.Value ?? throw new ArgumentNullException(nameof(options));
        _transactionLogService = transactionLogService ?? throw new ArgumentNullException(nameof(transactionLogService));
        _budgetService = budgetService ?? throw new ArgumentNullException(nameof(budgetService));
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

    public async Task<MarketItemDto> PlaceBidAsync(Guid itemId, string teamToken, decimal amount, uint expectedRowVersion, CancellationToken ct)
    {
        if (amount <= 0)
            throw new MarketValidationException("O valor do lance deve ser maior que zero.");

        EnsureExpectedRowVersion(expectedRowVersion);

        var normalizedToken = NormalizeToken(teamToken);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        return await InSerializableTxAsync<MarketItemDto>(async ct2 =>
        {
            var item = await _dbContext.MarketItems
                .Include(i => i.Cycle)
                .Include(i => i.Player).ThenInclude(p => p.Position)
                .FirstOrDefaultAsync(i => i.ItemId == itemId, ct2)
                ?? throw new MarketNotFoundException("Item de mercado não encontrado.");

            EnsureRowVersion(item.RowVersion, expectedRowVersion);

            if (item.Cycle is null || item.Cycle.Status != MarketCycleStatus.Active)
                throw new MarketConflictException("O ciclo do mercado não está ativo no momento.");

            if (item.Status != MarketItemStatus.Active)
                throw new MarketConflictException("O item não está disponível para lances.");

            var expiresUtc = DateTime.SpecifyKind(item.ExpiresAtUtc, DateTimeKind.Utc);
            if (expiresUtc <= nowUtc)
                throw new MarketConflictException("O item já expirou. Atualize a página e tente novamente.");

            var team = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.Token != null && t.Token.ToUpper() == normalizedToken, ct2)
                ?? throw new MarketForbiddenException("Token de time inválido.");

            if (item.CurrentLeaderTeamId == team.TeamId)
                throw new MarketValidationException("Sua equipe já lidera este item.");

            var requiredMinimum = MarketPricing.ComputeRequiredMinBid(
                item.BasePrice, item.MinIncrement, item.CurrentLeaderAmount, item.BuyNowPrice);

            var culture = CultureInfo.GetCultureInfo("pt-BR");
            var normalizedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

            if (normalizedAmount < requiredMinimum)
                throw new MarketValidationException($"O lance mínimo permitido é {requiredMinimum.ToString("C", culture)}.");

            if (item.BuyNowPrice.HasValue && normalizedAmount >= item.BuyNowPrice.Value)
            {
                var buyNowFormatted = item.BuyNowPrice.Value.ToString("C", culture);
                throw new MarketValidationException($"Utilize a opção de compra imediata para valores a partir de {buyNowFormatted}.");
            }

            await EnsureSquadLimitAsync(team.TeamId, item.ItemId, item.CurrentLeaderTeamId, ct2);

            var availableBudget = await _budgetService.GetAvailableAsync(team.TeamId, null, ct2).ConfigureAwait(false);
            if (availableBudget < normalizedAmount)
                throw new MarketValidationException("Saldo insuficiente para este lance considerando lances líderes ativos em outros itens.");

            var previousLeaderId = item.CurrentLeaderTeamId;
            var previousAmount = item.CurrentLeaderAmount ?? 0m;
            string? outbidNotes = null;

            if (previousLeaderId.HasValue)
            {
                var previousTeam = await _dbContext.Teams
                    .FirstOrDefaultAsync(t => t.TeamId == previousLeaderId.Value, ct2);

                if (previousTeam is not null)
                {
                    var previousRounded = decimal.Round(previousAmount, 2, MidpointRounding.AwayFromZero);
                    previousTeam.BudgetBlocked = Math.Max(0m, previousTeam.BudgetBlocked - previousRounded);
                    var previousTeamName = string.IsNullOrWhiteSpace(previousTeam.TeamName)
                        ? previousTeam.TeamId.ToString()
                        : previousTeam.TeamName;
                    outbidNotes = string.Format(culture, "Time {0} foi superado no leilão de {1}.", previousTeamName, item.Player.Name);
                }
                else
                {
                    outbidNotes = string.Format(culture, "Líder anterior não encontrado ao liberar orçamento do leilão de {0}.", item.Player.Name);
                }
            }

            team.BudgetBlocked += normalizedAmount;

            var bid = new MarketBid
            {
                BidId = Guid.NewGuid(),
                ItemId = item.ItemId,
                TeamId = team.TeamId,
                Amount = normalizedAmount,
                CreatedAtUtc = nowUtc
            };

            item.CurrentLeaderTeamId = team.TeamId;
            item.CurrentLeaderAmount = normalizedAmount;
            item.LastUpdateUtc = nowUtc;

            AdjustExpirationAfterBidUtc(item, previousLeaderId, nowUtc);

            var bidNotes = string.Format(culture, "Lance de {0:C} em {1} registrado.", normalizedAmount, item.Player.Name);
            if (!string.IsNullOrWhiteSpace(outbidNotes))
                bidNotes = $"{bidNotes} {outbidNotes}";

            await _transactionLogService.LogMarketAsync(
                item, MarketTransactionType.BidPlaced, team.TeamId, previousLeaderId,
                amount, team.TeamId.ToString(), bidNotes, nowUtc, ct2);

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

            await _dbContext.Entry(item).ReloadAsync(ct2);
            await _dbContext.Entry(item).Reference(i => i.Player).LoadAsync(ct2);
            if (item.Player is not null)
                await _dbContext.Entry(item.Player).Reference(p => p.Position).LoadAsync(ct2);
            await _dbContext.Entry(item).Reference(i => i.CurrentLeaderTeam).LoadAsync(ct2);

            return ToDto(item);
        }, ct);
    }

    public async Task<BuyNowResultDto> BuyNowAsync(Guid itemId, string teamToken, uint expectedRowVersion, CancellationToken ct)
    {
        EnsureExpectedRowVersion(expectedRowVersion);

        var normalizedToken = NormalizeToken(teamToken);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

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

            if (item.ExpiresAtUtc.AddHours(3) <= nowUtc)
                throw new MarketConflictException("O item já expirou. Atualize a página e tente novamente.");

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
            item.CurrentLeaderTeamId = team.TeamId;
            item.CurrentLeaderAmount = buyNowPrice;
            item.CurrentLeaderTeam = team;
            item.LastUpdateUtc = nowUtc;
            item.ExpiresAtUtc = nowUtc;

            var buyNowNotes = FormattableString.Invariant($"Compra imediata por {buyNowPrice:0.00}.");
            if (!string.IsNullOrWhiteSpace(takeoverNotes))
                buyNowNotes = $"{buyNowNotes} {takeoverNotes}";

            await _transactionLogService.LogMarketAsync(
                item, MarketTransactionType.BuyNow, team.TeamId, previousLeaderId,
                buyNowPrice, team.TeamId.ToString(), buyNowNotes, nowUtc, ct2);

            await _dbContext.TransferHistories.AddAsync(new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                CycleId = item.CycleId,
                PlayerId = player.PlayerId,
                FromTeamId = null,
                ToTeamId = team.TeamId,
                Amount = buyNowPrice,
                Type = TransferType.MarketAuction,
                Notes = "Compra imediata",
                PerformedBy = "sistema",
                PerformedAtUtc = nowUtc,
                OldOverall = player.Overall,
                NewOverall = player.Overall
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
            projected += 1;

        if (projected > SquadLimit)
            throw new MarketValidationException("O time atingiria o limite de 23 jogadores.");
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
                _dbContext.TeamRosters.Remove(entry);
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

    private static void AdjustExpirationAfterBidUtc(MarketItem item, Guid? previousLeaderId, DateTime nowUtcRaw)
    {
        if (item is null) throw new ArgumentNullException(nameof(item));

        var nowUtc = DateTime.SpecifyKind(nowUtcRaw, DateTimeKind.Utc);
        var expiresUtc = DateTime.SpecifyKind(item.ExpiresAtUtc, DateTimeKind.Utc);

        var isFirstBid = !previousLeaderId.HasValue || previousLeaderId.Value == Guid.Empty;

        if (isFirstBid)
        {
            var capUtc = nowUtc.AddHours(3);
            if (expiresUtc > capUtc)
                expiresUtc = capUtc;
        }

        var remaining = expiresUtc - nowUtc;
        if (remaining > TimeSpan.Zero && remaining <= TimeSpan.FromMinutes(30))
        {
            var ext = CalculateBidExtension(remaining);
            if (ext > TimeSpan.Zero)
                expiresUtc = expiresUtc.Add(ext);
        }

        item.ExpiresAtUtc = DateTime.SpecifyKind(expiresUtc, DateTimeKind.Utc);
    }

    private static TimeSpan CalculateBidExtension(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
            return TimeSpan.Zero;

        if (remaining <= TimeSpan.FromMinutes(5))
            return TimeSpan.FromMinutes(30);

        if (remaining <= TimeSpan.FromMinutes(10))
            return TimeSpan.FromMinutes(25);

        if (remaining <= TimeSpan.FromMinutes(15))
            return TimeSpan.FromMinutes(20);

        if (remaining <= TimeSpan.FromMinutes(20))
            return TimeSpan.FromMinutes(15);
        

        if (remaining <= TimeSpan.FromMinutes(25))
            return TimeSpan.FromMinutes(10);

        if (remaining <= TimeSpan.FromMinutes(30))
            return TimeSpan.FromMinutes(5);

        return TimeSpan.Zero;
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new MarketForbiddenException("Token de time é obrigatório.");

        return token.Trim().ToUpperInvariant();
    }

    private static MarketItemDto ToDto(MarketItem item)
    {
        var positionName = item.Player.Position?.Name ?? string.Empty;
        var age = item.Player.Age ?? 0;
        var leaderName = item.CurrentLeaderTeam is not null && !string.IsNullOrWhiteSpace(item.CurrentLeaderTeam.TeamName)
            ? item.CurrentLeaderTeam.TeamName
            : item.CurrentLeaderTeamId?.ToString();

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
            MarketPricing.ComputeRequiredMinBid(item.BasePrice, item.MinIncrement, item.CurrentLeaderAmount, item.BuyNowPrice),
            item.ExpiresAtUtc,
            item.Status.ToString(),
            item.CurrentLeaderAmount,
            leaderName,
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
            throw new MarketPreconditionFailedException("A versão informada do item é inválida.");
    }

    private static void EnsureRowVersion(uint current, uint expected)
    {
        if (current != expected)
            throw new MarketPreconditionFailedException("O item foi atualizado por outro time. Atualize a página e tente novamente.");
    }

    public async Task<T> InSerializableTxAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

            var result = await action(ct);

            await tx.CommitAsync(ct);
            return result;
        });
    }
}
