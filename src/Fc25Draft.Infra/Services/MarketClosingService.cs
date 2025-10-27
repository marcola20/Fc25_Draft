using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

public class MarketClosingService : IMarketClosingService
{
    private readonly DraftDbContext _dbContext;
    private readonly ILogger<MarketClosingService> _logger;
    private readonly TimeProvider _timeProvider;

    public MarketClosingService(
        DraftDbContext dbContext,
        ILogger<MarketClosingService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MarketClosePreviewDto> PreviewCloseAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var items = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Where(i => i.Status == "OPEN")
            .Include(i => i.Player)
                .ThenInclude(p => p.Position)
            .Include(i => i.Player)
                .ThenInclude(p => p.TeamRosters)
            .Include(i => i.Bids)
                .ThenInclude(b => b.Team)
            .OrderBy(i => i.DataInicioUtc)
            .ThenBy(i => i.MarketItemId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var teamIds = items
            .SelectMany(i => i.Bids.Select(b => b.TeamId))
            .Distinct()
            .ToList();

        var budgetSnapshots = await LoadBudgetSnapshotsAsync(teamIds, ct).ConfigureAwait(false);

        var previews = new List<ItemPreviewDto>(items.Count);

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();

            var orderedBids = OrderBids(item.Bids);
            var highestBid = orderedBids.FirstOrDefault();
            var highestBidValue = highestBid?.Valor;
            var highestBidTeamName = highestBid?.Team?.TeamName;

            var playerHasTeam = item.Player.TeamRosters.Any();
            bool hasEligibleWinner = false;
            string decision;

            if (playerHasTeam)
            {
                decision = "EXPIRE";
            }
            else if (!orderedBids.Any())
            {
                decision = "EXPIRE";
            }
            else
            {
                Bid? eligible = null;
                var evaluatedTeams = new HashSet<Guid>();
                foreach (var bid in orderedBids)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!evaluatedTeams.Add(bid.TeamId))
                    {
                        continue;
                    }

                    var snapshot = budgetSnapshots.TryGetValue(bid.TeamId, out var value)
                        ? value
                        : BudgetSnapshot.Empty;

                    var isCurrentLeader = item.MaiorLanceTeamId == bid.TeamId;
                    var disponivel = snapshot.Disponivel;
                    var required = isCurrentLeader ? 0m : bid.Valor;

                    if (disponivel >= required)
                    {
                        eligible = bid;
                        break;
                    }
                }

                if (eligible is not null)
                {
                    hasEligibleWinner = true;
                    var teamName = eligible.Team?.TeamName ?? string.Empty;
                    decision = $"SELL to {teamName}";
                }
                else
                {
                    decision = "NO_ELIGIBLE";
                }
            }

            var player = item.Player;
            var positionName = player.Position?.Name ?? string.Empty;
            var age = player.Age ?? 0;

            previews.Add(new ItemPreviewDto(
                item.MarketItemId,
                item.PlayerId,
                player.Name,
                positionName,
                age,
                player.Overall,
                highestBidValue,
                highestBidTeamName,
                hasEligibleWinner,
                decision));
        }

        return new MarketClosePreviewDto(items.Count, previews);
    }

    public async Task<MarketCloseResultDto> CloseRoundAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var itemIds = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Where(i => i.Status == "OPEN")
            .OrderBy(i => i.DataInicioUtc)
            .ThenBy(i => i.MarketItemId)
            .Select(i => i.MarketItemId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var results = new List<ItemCloseResultDto>(itemIds.Count);
        var sold = 0;
        var expired = 0;

        foreach (var itemId in itemIds)
        {
            ct.ThrowIfCancellationRequested();

            var result = await CloseSingleItemAsync(itemId, ct, treatNotOpenAsNotFound: false).ConfigureAwait(false);
            results.Add(result);

            if (string.Equals(result.StatusAfter, "SOLD", StringComparison.OrdinalIgnoreCase))
            {
                sold++;
            }
            else if (string.Equals(result.StatusAfter, "EXPIRED", StringComparison.OrdinalIgnoreCase))
            {
                expired++;
            }
        }

        return new MarketCloseResultDto(itemIds.Count, sold, expired, results);
    }

    public Task<ItemCloseResultDto> CloseItemAsync(Guid marketItemId, CancellationToken ct)
    {
        return CloseSingleItemAsync(marketItemId, ct, treatNotOpenAsNotFound: true);
    }

    private async Task<ItemCloseResultDto> CloseSingleItemAsync(Guid marketItemId, CancellationToken ct, bool treatNotOpenAsNotFound)
    {
        ct.ThrowIfCancellationRequested();

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct)
            .ConfigureAwait(false);

        var item = await _dbContext.TransferMarketItems
            .Include(i => i.Player)
                .ThenInclude(p => p.Position)
            .Include(i => i.Bids)
                .ThenInclude(b => b.Team)
            .FirstOrDefaultAsync(i => i.MarketItemId == marketItemId, ct)
            .ConfigureAwait(false);

        if (item is null)
        {
            throw new TransferMarketNotFoundException("Item de mercado não encontrado.");
        }

        if (!string.Equals(item.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            if (treatNotOpenAsNotFound)
            {
                throw new TransferMarketNotFoundException("Item de mercado não está aberto.");
            }

            throw new TransferMarketConflictException("O estado do item mudou durante o fechamento. Tente novamente.");
        }

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        var playerAlreadyInRoster = await _dbContext.TeamRosters
            .AsNoTracking()
            .AnyAsync(r => r.PlayerId == item.PlayerId, ct)
            .ConfigureAwait(false);

        ItemCloseResultDto result;

        if (playerAlreadyInRoster)
        {
            item.Status = "EXPIRED";
            item.DataFimUtc = nowUtc;
            item.VencedorTeamId = null;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            const string message = "EXPIRED (jogador indisponível)";
            _logger.LogInformation("Market item {MarketItemId}: {Message}", item.MarketItemId, message);
            result = new ItemCloseResultDto(item.MarketItemId, "EXPIRED", null, null, message);
            return result;
        }

        var orderedBids = OrderBids(item.Bids);

        if (!orderedBids.Any())
        {
            item.Status = "EXPIRED";
            item.DataFimUtc = nowUtc;
            item.VencedorTeamId = null;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            const string message = "EXPIRED (sem lances)";
            _logger.LogInformation("Market item {MarketItemId}: {Message}", item.MarketItemId, message);
            result = new ItemCloseResultDto(item.MarketItemId, "EXPIRED", null, null, message);
            return result;
        }

        var teamIds = orderedBids
            .Select(b => b.TeamId)
            .Distinct()
            .ToList();

        var budgetSnapshots = await LoadBudgetSnapshotsAsync(teamIds, ct).ConfigureAwait(false);

        Bid? winnerBid = null;
        var evaluated = new HashSet<Guid>();
        foreach (var bid in orderedBids)
        {
            ct.ThrowIfCancellationRequested();

            if (!evaluated.Add(bid.TeamId))
            {
                continue;
            }

            var snapshot = budgetSnapshots.TryGetValue(bid.TeamId, out var value)
                ? value
                : BudgetSnapshot.Empty;

            var isCurrentLeader = item.MaiorLanceTeamId == bid.TeamId;
            var disponivel = snapshot.Disponivel;
            var required = isCurrentLeader ? 0m : bid.Valor;

            if (disponivel >= required)
            {
                winnerBid = bid;
                break;
            }
        }

        if (winnerBid is null)
        {
            item.Status = "EXPIRED";
            item.DataFimUtc = nowUtc;
            item.VencedorTeamId = null;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            const string message = "EXPIRED (sem elegíveis)";
            _logger.LogInformation("Market item {MarketItemId}: {Message}", item.MarketItemId, message);
            result = new ItemCloseResultDto(item.MarketItemId, "EXPIRED", null, null, message);
            return result;
        }

        var winnerTeamId = winnerBid.TeamId;
        var winnerTeamName = winnerBid.Team?.TeamName ?? string.Empty;
        var winnerValue = winnerBid.Valor;

        var rosterConflict = await _dbContext.TeamRosters
            .AsNoTracking()
            .AnyAsync(r => r.PlayerId == item.PlayerId, ct)
            .ConfigureAwait(false);

        if (rosterConflict)
        {
            item.Status = "EXPIRED";
            item.DataFimUtc = nowUtc;
            item.VencedorTeamId = null;

            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);

            const string message = "EXPIRED (jogador indisponível)";
            _logger.LogInformation("Market item {MarketItemId}: {Message}", item.MarketItemId, message);
            result = new ItemCloseResultDto(item.MarketItemId, "EXPIRED", null, null, message);
            return result;
        }

        item.Status = "SOLD";
        item.DataFimUtc = nowUtc;
        item.VencedorTeamId = winnerTeamId;
        item.MaiorLanceTeamId = winnerTeamId;
        item.LanceAtual = winnerValue;

        await DebitTeamBudgetAsync(winnerTeamId, winnerValue, ct).ConfigureAwait(false);

        var transferHistory = new TransferHistory
        {
            TransferHistoryId = Guid.NewGuid(),
            PlayerId = item.PlayerId,
            OrigemTeamId = null,
            DestinoTeamId = winnerTeamId,
            Valor = winnerValue,
            Tipo = "MARKET_AUCTION",
            DataUtc = nowUtc
        };

        var rosterEntry = new TeamRoster
        {
            TeamId = winnerTeamId,
            PlayerId = item.PlayerId
        };

        _dbContext.TransferHistories.Add(transferHistory);
        _dbContext.TeamRosters.Add(rosterEntry);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        var messageSold = $"SOLD to {winnerTeamName} por {winnerValue.ToString("0.0", CultureInfo.InvariantCulture)}";
        _logger.LogInformation("Market item {MarketItemId}: {Message}", item.MarketItemId, messageSold);

        result = new ItemCloseResultDto(
            item.MarketItemId,
            "SOLD",
            winnerTeamName,
            winnerValue,
            messageSold);

        return result;
    }

    private static List<Bid> OrderBids(IEnumerable<Bid> bids)
    {
        return bids
            .OrderByDescending(b => b.Valor)
            .ThenBy(b => b.DataUtc)
            .ToList();
    }

    private async Task<Dictionary<Guid, BudgetSnapshot>> LoadBudgetSnapshotsAsync(IEnumerable<Guid> teamIds, CancellationToken ct)
    {
        var ids = teamIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, BudgetSnapshot>();
        }

        var budgets = await _dbContext.TeamBudgets
            .AsNoTracking()
            .Where(b => ids.Contains(b.TeamId))
            .Select(b => new { b.TeamId, b.Saldo })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var budgetMap = budgets.ToDictionary(x => x.TeamId, x => x.Saldo);

        var bloqueios = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Where(m => m.Status == "OPEN" && m.LanceAtual != null && m.MaiorLanceTeamId != null && ids.Contains(m.MaiorLanceTeamId.Value))
            .GroupBy(m => m.MaiorLanceTeamId!.Value)
            .Select(g => new { TeamId = g.Key, Total = g.Sum(x => x.LanceAtual!.Value) })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var bloqueioMap = bloqueios.ToDictionary(x => x.TeamId, x => x.Total);

        var result = new Dictionary<Guid, BudgetSnapshot>(ids.Count);
        foreach (var id in ids)
        {
            var saldo = budgetMap.TryGetValue(id, out var s) ? s : 0m;
            var bloqueado = bloqueioMap.TryGetValue(id, out var b) ? b : 0m;
            result[id] = new BudgetSnapshot(saldo, bloqueado);
        }

        return result;
    }

    private async Task DebitTeamBudgetAsync(Guid teamId, decimal value, CancellationToken ct)
    {
        var budget = await _dbContext.TeamBudgets
            .FirstOrDefaultAsync(b => b.TeamId == teamId, ct)
            .ConfigureAwait(false);

        if (budget is null)
        {
            throw new InvalidOperationException("Saldo do time não encontrado.");
        }

        budget.Saldo -= value;
    }

    private readonly record struct BudgetSnapshot(decimal Saldo, decimal Bloqueado)
    {
        public static BudgetSnapshot Empty => new(0m, 0m);
        public decimal Disponivel => Saldo - Bloqueado;
    }
}
