using System.Linq.Expressions;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class MarketHistoryQueryService : IMarketHistoryQueryService
{
    private const int MaxPageSize = 200;
    private const int MaxExportRecords = 5000;
    private readonly DraftDbContext _dbContext;

    public MarketHistoryQueryService(DraftDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PagedResult<MarketTransactionDto>> QueryAsync(MarketHistoryFilter filter, CancellationToken ct)
    {
        ValidateFilter(filter);

        var query = BuildQuery(filter);

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var pageSize = Math.Min(filter.PageSize, MaxPageSize);
        var skip = (filter.Page - 1) * pageSize;

        var items = await query
            .OrderByDescending(x => x.Transaction.CreatedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .Select(SelectProjection())
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<MarketTransactionDto>(items, total);
    }

    public async Task<IReadOnlyList<MarketTransactionDto>> ExportAsync(MarketHistoryFilter filter, CancellationToken ct)
    {
        ValidateFilter(filter, allowZeroPage: true);

        var query = BuildQuery(filter);

        var items = await query
            .OrderByDescending(x => x.Transaction.CreatedAtUtc)
            .Take(MaxExportRecords)
            .Select(SelectProjection())
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return items;
    }

    private void ValidateFilter(MarketHistoryFilter filter, bool allowZeroPage = false)
    {
        if (filter is null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        if (!allowZeroPage && filter.Page < 1)
        {
            throw new ArgumentException("Página deve ser maior ou igual a 1.", nameof(filter));
        }

        if (filter.PageSize < 1)
        {
            throw new ArgumentException("Tamanho da página deve ser maior ou igual a 1.", nameof(filter));
        }
    }

    private IQueryable<QueryModel> BuildQuery(MarketHistoryFilter filter)
    {
        var baseQuery = from transaction in _dbContext.MarketTransactions.AsNoTracking()
                        join player in _dbContext.Players.AsNoTracking() on transaction.PlayerId equals player.PlayerId
                        join position in _dbContext.Positions.AsNoTracking() on player.PositionId equals position.PositionId
                        join team in _dbContext.Teams.AsNoTracking() on transaction.TeamId equals team.TeamId into teamJoin
                        from team in teamJoin.DefaultIfEmpty()
                        join targetTeam in _dbContext.Teams.AsNoTracking() on transaction.TargetTeamId equals targetTeam.TeamId into targetJoin
                        from targetTeam in targetJoin.DefaultIfEmpty()
                        select new QueryModel(transaction, player, position, team, targetTeam);

        if (filter.CycleId.HasValue)
        {
            var cycleId = filter.CycleId.Value;
            baseQuery = baseQuery.Where(x => x.Transaction.CycleId == cycleId);
        }

        if (filter.ItemId.HasValue)
        {
            var itemId = filter.ItemId.Value;
            baseQuery = baseQuery.Where(x => x.Transaction.ItemId == itemId);
        }

        if (filter.PlayerId.HasValue)
        {
            var playerId = filter.PlayerId.Value;
            baseQuery = baseQuery.Where(x => x.Transaction.PlayerId == playerId);
        }

        if (!string.IsNullOrWhiteSpace(filter.PlayerName))
        {
            var pattern = $"%{filter.PlayerName.Trim()}%";
            baseQuery = baseQuery.Where(x => EF.Functions.ILike(x.Player.Name, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.TeamName))
        {
            var pattern = $"%{filter.TeamName.Trim()}%";
            baseQuery = baseQuery.Where(x =>
                (x.Team != null && EF.Functions.ILike(x.Team.TeamName, pattern)) ||
                (x.TargetTeam != null && EF.Functions.ILike(x.TargetTeam.TeamName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetTeamName))
        {
            var pattern = $"%{filter.TargetTeamName.Trim()}%";
            baseQuery = baseQuery.Where(x => x.TargetTeam != null && EF.Functions.ILike(x.TargetTeam.TeamName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.PerformedBy))
        {
            var pattern = $"%{filter.PerformedBy.Trim()}%";
            baseQuery = baseQuery.Where(x => EF.Functions.ILike(x.Transaction.PerformedBy, pattern));
        }

        if (filter.Type.HasValue)
        {
            var type = filter.Type.Value;
            baseQuery = baseQuery.Where(x => x.Transaction.Type == type);
        }

        if (filter.FromUtc.HasValue)
        {
            var from = EnsureUtc(filter.FromUtc.Value);
            baseQuery = baseQuery.Where(x => x.Transaction.CreatedAtUtc >= from);
        }

        if (filter.ToUtc.HasValue)
        {
            var to = EnsureUtc(filter.ToUtc.Value);
            baseQuery = baseQuery.Where(x => x.Transaction.CreatedAtUtc <= to);
        }

        return baseQuery;
    }

    private static Expression<Func<QueryModel, MarketTransactionDto>> SelectProjection() => x => new MarketTransactionDto(
        x.Transaction.TransactionId,
        x.Transaction.CycleId,
        x.Transaction.ItemId,
        x.Transaction.PlayerId,
        x.Player.Name,
        x.Position.Name,
        x.Transaction.TeamId,
        x.Team != null ? x.Team.TeamName : null,
        x.Transaction.TargetTeamId,
        x.TargetTeam != null ? x.TargetTeam.TeamName : null,
        x.Transaction.Type,
        x.Transaction.Type.ToDisplayName(),
        x.Transaction.Amount,
        x.Transaction.PerformedBy,
        x.Transaction.Notes,
        x.Transaction.CreatedAtUtc);

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private sealed record QueryModel(
        Core.Entities.MarketTransaction Transaction,
        Core.Entities.Player Player,
        Core.Entities.Position Position,
        Core.Entities.Team? Team,
        Core.Entities.Team? TargetTeam);
}
