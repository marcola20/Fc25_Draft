using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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

        var q = BuildQuery();
        q = ApplyFilter(q, filter);

        var total = await q.CountAsync(ct);
        var pageSize = Math.Min(filter.PageSize, MaxPageSize);
        var skip = (filter.Page - 1) * pageSize;

        var items = await q
            .OrderByDescending(x => x.Transaction.CreatedAtUtc)
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new MarketTransactionDto(
                x.Transaction.TransactionId,
                x.Transaction.CycleId,
                x.Transaction.ItemId,
                x.Transaction.PlayerId,
                x.Player.Name,
                x.Position.Name,
                x.Transaction.TeamId,
                x.FromTeam != null ? x.FromTeam.TeamName : null,
                x.Transaction.TargetTeamId,
                x.ToTeam != null ? x.ToTeam.TeamName : null,
                x.Transaction.Type,
                x.Transaction.Type.ToDisplayName(),
                x.Transaction.Amount,
                x.Transaction.PerformedBy,
                x.Transaction.Notes,
                x.Transaction.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<MarketTransactionDto>(items, total, filter.Page, pageSize);
    }


    public async Task<IReadOnlyList<MarketTransactionDto>> ExportAsync(MarketHistoryFilter filter, CancellationToken ct)
    {
        ValidateFilter(filter, allowZeroPage: true);

        var q = BuildQuery();
        q = ApplyFilter(q, filter);

        var items = await q
            .OrderByDescending(x => x.Transaction.CreatedAtUtc)
            .Take(MaxExportRecords)
            .Select(x => new MarketTransactionDto(
                x.Transaction.TransactionId,
                x.Transaction.CycleId,
                x.Transaction.ItemId,
                x.Transaction.PlayerId,
                x.Player.Name,
                x.Position.Name,
                x.Transaction.TeamId,
                x.FromTeam != null ? x.FromTeam.TeamName : null,
                x.Transaction.TargetTeamId,
                x.ToTeam != null ? x.ToTeam.TeamName : null,
                x.Transaction.Type,
                x.Transaction.Type.ToDisplayName(),
                x.Transaction.Amount,
                x.Transaction.PerformedBy,
                x.Transaction.Notes,
                x.Transaction.CreatedAtUtc))
            .ToListAsync(ct);

        return items;
    }

    private IQueryable<QueryProjection> BuildQuery()
        =>
            from m in _dbContext.MarketTransactions.AsNoTracking()
            join p in _dbContext.Players.AsNoTracking() on m.PlayerId equals p.PlayerId
            join pos in _dbContext.Positions.AsNoTracking() on p.PositionId equals pos.PositionId
            join tf0 in _dbContext.Teams.AsNoTracking() on m.TeamId equals tf0.TeamId into gjFrom
            from tf in gjFrom.DefaultIfEmpty()
            join tt0 in _dbContext.Teams.AsNoTracking() on m.TargetTeamId equals tt0.TeamId into gjTo
            from tt in gjTo.DefaultIfEmpty()
            select new QueryProjection(m, p, pos, tf, tt);

    private static IQueryable<QueryProjection> ApplyFilter(IQueryable<QueryProjection> query, MarketHistoryFilter filter)
    {
        if (filter.CycleId.HasValue)
        {
            var cycleId = filter.CycleId.Value;
            query = query.Where(x => x.Transaction.CycleId == cycleId);
        }

        if (filter.ItemId.HasValue)
        {
            var itemId = filter.ItemId.Value;
            query = query.Where(x => x.Transaction.ItemId == itemId);
        }

        if (filter.TeamId.HasValue)
        {
            var teamId = filter.TeamId.Value;
            query = query.Where(x => x.Transaction.TeamId == teamId || x.Transaction.TargetTeamId == teamId);
        }

        if (filter.PlayerId.HasValue)
        {
            var playerId = filter.PlayerId.Value;
            query = query.Where(x => x.Transaction.PlayerId == playerId);
        }

        if (!string.IsNullOrWhiteSpace(filter.PlayerName))
        {
            var pat = $"%{filter.PlayerName.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Player.Name, pat));
        }

        if (!string.IsNullOrWhiteSpace(filter.TeamName))
        {
            var pat = $"%{filter.TeamName.Trim()}%";
            query = query.Where(x =>
                (x.FromTeam != null && EF.Functions.ILike(x.FromTeam.TeamName, pat)) ||
                (x.ToTeam != null && EF.Functions.ILike(x.ToTeam.TeamName, pat)));
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetTeamName))
        {
            var pat = $"%{filter.TargetTeamName.Trim()}%";
            query = query.Where(x => x.ToTeam != null && EF.Functions.ILike(x.ToTeam.TeamName, pat));
        }

        if (!string.IsNullOrWhiteSpace(filter.PerformedBy))
        {
            var pat = $"%{filter.PerformedBy.Trim()}%";
            query = query.Where(x => EF.Functions.ILike(x.Transaction.PerformedBy, pat));
        }

        if (filter.Type.HasValue)
        {
            var type = filter.Type.Value;
            query = query.Where(x => x.Transaction.Type == type);
        }

        if (filter.FromUtc.HasValue)
        {
            var from = EnsureUtc(filter.FromUtc.Value);
            query = query.Where(x => x.Transaction.CreatedAtUtc >= from);
        }

        if (filter.ToUtc.HasValue)
        {
            var to = EnsureUtc(filter.ToUtc.Value);
            query = query.Where(x => x.Transaction.CreatedAtUtc <= to);
        }

        return query;
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

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    private sealed record QueryProjection(
        MarketTransaction Transaction,
        Player Player,
        Position Position,
        Team? FromTeam,
        Team? ToTeam);
}
