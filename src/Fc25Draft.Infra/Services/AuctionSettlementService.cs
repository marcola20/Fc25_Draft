using System;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
        => SettleAsync(cycleId, onlyExpired: true, ct);

    public Task<AuctionSettlementResult> SettleAllOpenItemsOnCycleCloseAsync(Guid cycleId, CancellationToken ct)
        => SettleAsync(cycleId, onlyExpired: false, ct);

    private async Task<AuctionSettlementResult> SettleAsync(Guid cycleId, bool onlyExpired, CancellationToken ct)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var candidatesQuery = _dbContext.MarketItems
            .AsNoTracking()
            .Where(i => i.CycleId == cycleId)
            .Where(i => i.Status != MarketItemStatus.Sold && i.Status != MarketItemStatus.Canceled);

        if (onlyExpired)
        {
            candidatesQuery = candidatesQuery.Where(i => i.ExpiresAtUtc <= now);
        }

        var itemIds = await candidatesQuery
            .Select(i => i.ItemId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var sold = 0;
        var expired = 0;
        foreach (var itemId in itemIds)
        {
            try
            {
                var outcome = await SettleSingleAsync(itemId, now, onlyExpired, ct).ConfigureAwait(false);
                switch (outcome)
                {
                    case SettlementOutcome.Sold:
                        sold++;
                        break;
                    case SettlementOutcome.Expired:
                        expired++;
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao finalizar item {ItemId}", itemId);
            }
        }

        return new AuctionSettlementResult(sold, expired);
    }

    private async Task<SettlementOutcome> SettleSingleAsync(Guid itemId, DateTime now, bool onlyExpired, CancellationToken ct)
    {
        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct)
            .ConfigureAwait(false);

        var item = await _dbContext.MarketItems
            .Include(i => i.Player)
            .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
            .ConfigureAwait(false);

        if (item is null)
        {
            return SettlementOutcome.None;
        }

        if (item.Status == MarketItemStatus.Sold || item.Status == MarketItemStatus.Canceled)
        {
            return SettlementOutcome.None;
        }

        if (onlyExpired && item.ExpiresAtUtc > now)
        {
            return SettlementOutcome.None;
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

                item.LastUpdateUtc = now;
                item.ExpiresAtUtc = now;

                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);
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

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
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

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return SettlementOutcome.Expired;
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
        Expired
    }
}
