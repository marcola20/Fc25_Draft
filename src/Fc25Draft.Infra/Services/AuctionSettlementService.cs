using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Fc25Draft.Infra.Services;

public class AuctionSettlementService : IAuctionSettlementService
{
    private const int SquadLimit = 23;

    private readonly DraftDbContext _dbContext;
    private readonly ITransactionLogService _transactionLogService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuctionSettlementService> _logger;

    public AuctionSettlementService(
        DraftDbContext dbContext,
        ITransactionLogService transactionLogService,
        ILogger<AuctionSettlementService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _transactionLogService = transactionLogService ?? throw new ArgumentNullException(nameof(transactionLogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public Task<AuctionSettlementResult> SettleExpiredItemsAsync(Guid cycleId, CancellationToken ct)
        => SettleAsync(cycleId, onlyExpired: true, forceClose: false, ct);

    public Task<AuctionSettlementResult> SettleAllOpenItemsOnCycleCloseAsync(Guid cycleId, bool forceClose, CancellationToken ct)
        => SettleAsync(cycleId, onlyExpired: false, forceClose: forceClose, ct);

    private async Task<AuctionSettlementResult> SettleAsync(Guid cycleId, bool onlyExpired, bool forceClose, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct)
                .ConfigureAwait(false);

            var itemsQuery = _dbContext.MarketItems
                .Include(i => i.Player)
                .Where(i => i.CycleId == cycleId)
                .Where(i => i.Status != MarketItemStatus.Sold && i.Status != MarketItemStatus.Canceled);

            if (onlyExpired)
            {
                var tz = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time")
                    : TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");

                var nowBr = TimeZoneInfo.ConvertTimeFromUtc(now, tz);
                itemsQuery = itemsQuery.Where(i => i.ExpiresAtUtc <= nowBr);
            }


            var items = await itemsQuery
                .OrderBy(i => i.CreatedAtUtc)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var sold = 0;
            var expired = 0;
            var canceled = 0;

            foreach (var item in items)
            {
                try
                {
                    var outcome = await ProcessItemAsync(item, now, onlyExpired, forceClose, ct).ConfigureAwait(false);

                    switch (outcome)
                    {
                        case SettlementOutcome.Sold:
                            sold++;
                            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                            break;
                        case SettlementOutcome.Expired:
                            expired++;
                            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                            break;
                        case SettlementOutcome.Canceled:
                            canceled++;
                            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao finalizar item {ItemId}", item.ItemId);
                    throw;
                }
            }

            if (!onlyExpired)
            {
                var cycle = await _dbContext.MarketCycles
                    .FirstOrDefaultAsync(c => c.CycleId == cycleId, ct)
                    .ConfigureAwait(false)
                    ?? throw new MarketNotFoundException("Ciclo não encontrado.");

                cycle.Status = MarketCycleStatus.Closed;
                cycle.UpdatedAtUtc = now;
                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            }
            else
            {
                await UpdateCycleStatusAsync(cycleId, now, ct).ConfigureAwait(false);
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);
            return new AuctionSettlementResult(sold, expired, canceled);
        });
    }

    private async Task<SettlementOutcome> ProcessItemAsync(MarketItem item, DateTime now, bool onlyExpired, bool forceClose, CancellationToken ct)
    {
        if (item.Status == MarketItemStatus.Sold || item.Status == MarketItemStatus.Canceled)
        {
            return SettlementOutcome.None;
        }

        if (forceClose)
        {
            return await CancelItemAsync(item, now, ct).ConfigureAwait(false);
        }

        if (onlyExpired && item.ExpiresAtUtc > now)
        {
            return SettlementOutcome.None;
        }

        if (item.Player is null)
        {
            item.Player = await _dbContext.Players
                .Include(p => p.Position)
                .FirstOrDefaultAsync(p => p.PlayerId == item.PlayerId, ct)
                .ConfigureAwait(false)
                ?? throw new MarketNotFoundException("Jogador não encontrado para o item do mercado.");
        }

        var previousLeaderId = item.CurrentLeaderTeamId;
        var previousLeaderAmount = item.CurrentLeaderAmount;
        var culture = CultureInfo.GetCultureInfo("pt-BR");

        if (item.CurrentLeaderTeamId.HasValue && item.CurrentLeaderAmount.HasValue)
        {
            var teamId = item.CurrentLeaderTeamId.Value;
            var amount = decimal.Round(item.CurrentLeaderAmount.Value, 2, MidpointRounding.AwayFromZero);

            var team = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
                .ConfigureAwait(false);

            if (team is null)
            {
                item.Status = MarketItemStatus.Expired;
                item.CurrentLeaderTeamId = null;
                item.CurrentLeaderAmount = null;
                item.WinnerTeamId = null;
                item.LastUpdateUtc = now;
                item.ExpiresAtUtc = now;

                await _transactionLogService.LogMarketAsync(
                    item,
                    MarketTransactionType.AuctionExpired,
                    null,
                    previousLeaderId,
                    previousLeaderAmount,
                    "sistema",
                    $"Leilão do jogador {item.Player.Name} encerrado sem vencedor válido.",
                    now,
                    ct).ConfigureAwait(false);

                return SettlementOutcome.Expired;
            }

            await EnsureSquadLimitAsync(team.TeamId, item.ItemId, ct).ConfigureAwait(false);

            team.BudgetBlocked = Math.Max(0m, team.BudgetBlocked - amount);
            team.Budget -= amount;
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
            item.CurrentLeaderTeamId = team.TeamId;
            item.CurrentLeaderAmount = amount;
            item.LastUpdateUtc = now;
            item.ExpiresAtUtc = now;

            await _dbContext.TransferHistories.AddAsync(new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                PlayerId = item.PlayerId,
                FromTeamId = null,
                ToTeamId = team.TeamId,
                Amount = amount,
                Type = TransferType.MarketAuction,
                Notes = $"Leilão encerrado por {amount.ToString("C", culture)}.",
                PerformedBy = "sistema",
                PerformedAtUtc = now
            }, ct).ConfigureAwait(false);

            var winnerName = string.IsNullOrWhiteSpace(team.TeamName) ? team.TeamId.ToString() : team.TeamName;
            var settleNotes = $"{item.Player.Name} arrematado por {winnerName} por {amount.ToString("C", culture)}.";

            await _transactionLogService.LogMarketAsync(
                item,
                MarketTransactionType.AuctionSettled,
                team.TeamId,
                previousLeaderId,
                amount,
                "sistema",
                settleNotes,
                now,
                ct).ConfigureAwait(false);

            return SettlementOutcome.Sold;
        }

        if (item.Status == MarketItemStatus.Expired)
        {
            return SettlementOutcome.None;
        }

        item.Status = MarketItemStatus.Expired;
        item.CurrentLeaderTeamId = null;
        item.CurrentLeaderAmount = null;
        item.WinnerTeamId = null;
        item.LastUpdateUtc = now;
        item.ExpiresAtUtc = now;

        await _transactionLogService.LogMarketAsync(
            item,
            MarketTransactionType.AuctionExpired,
            null,
            null,
            null,
            "sistema",
            $"Leilão do jogador {item.Player.Name} expirou sem lances válidos.",
            now,
            ct).ConfigureAwait(false);

        return SettlementOutcome.Expired;
    }

    private async Task<SettlementOutcome> CancelItemAsync(MarketItem item, DateTime now, CancellationToken ct)
    {
        var previousLeaderId = item.CurrentLeaderTeamId;
        var previousLeaderAmount = item.CurrentLeaderAmount;

        decimal? roundedAmount = null;

        if (previousLeaderId.HasValue && previousLeaderAmount.HasValue)
        {
            var team = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.TeamId == previousLeaderId.Value, ct)
                .ConfigureAwait(false);

            if (team is not null)
            {
                roundedAmount = decimal.Round(previousLeaderAmount.Value, 2, MidpointRounding.AwayFromZero);
                team.BudgetBlocked = Math.Max(0m, team.BudgetBlocked - roundedAmount.Value);
            }
        }

        item.Status = MarketItemStatus.Canceled;
        item.CurrentLeaderTeamId = null;
        item.CurrentLeaderAmount = null;
        item.WinnerTeamId = null;
        item.LastUpdateUtc = now;
        item.ExpiresAtUtc = now;

        var playerName = item.Player?.Name ?? item.PlayerId.ToString();

        await _transactionLogService.LogMarketAsync(
            item,
            MarketTransactionType.ItemCanceled,
            null,
            previousLeaderId,
            roundedAmount,
            "sistema",
            $"Leilão do jogador {playerName} cancelado no encerramento do ciclo.",
            now,
            ct).ConfigureAwait(false);

        return SettlementOutcome.Canceled;
    }

    private async Task UpdateCycleStatusAsync(Guid cycleId, DateTime now, CancellationToken ct)
    {
        var hasActive = await _dbContext.MarketItems
            .AsNoTracking()
            .AnyAsync(i => i.CycleId == cycleId && i.Status == MarketItemStatus.Active, ct)
            .ConfigureAwait(false);

        if (hasActive)
        {
            return;
        }

        var cycle = await _dbContext.MarketCycles
            .FirstOrDefaultAsync(c => c.CycleId == cycleId, ct)
            .ConfigureAwait(false);

        if (cycle is null || cycle.Status == MarketCycleStatus.Closed)
        {
            return;
        }

        cycle.Status = MarketCycleStatus.Closed;
        cycle.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private async Task EnsureSquadLimitAsync(Guid teamId, Guid currentItemId, CancellationToken ct)
    {
        var currentPlayers = await _dbContext.Players
            .AsNoTracking()
            .CountAsync(p => p.CurrentTeamId == teamId, ct)
            .ConfigureAwait(false);

        var activeLeads = await _dbContext.MarketItems
            .AsNoTracking()
            .CountAsync(i => i.Status == MarketItemStatus.Active && i.CurrentLeaderTeamId == teamId && i.ItemId != currentItemId, ct)
            .ConfigureAwait(false);

        if (currentPlayers + activeLeads + 1 > SquadLimit)
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

    private enum SettlementOutcome
    {
        None,
        Sold,
        Expired,
        Canceled
    }
}
