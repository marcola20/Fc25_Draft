using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Linq.Expressions;

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

        var q =
            from m in _dbContext.MarketTransactions.AsNoTracking()
            join p in _dbContext.Players.AsNoTracking() on m.PlayerId equals p.PlayerId
            join pos in _dbContext.Positions.AsNoTracking() on p.PositionId equals pos.PositionId
            join tf0 in _dbContext.Teams.AsNoTracking() on m.TeamId equals tf0.TeamId into gjFrom
            from tf in gjFrom.DefaultIfEmpty()
            join tt0 in _dbContext.Teams.AsNoTracking() on m.TargetTeamId equals tt0.TeamId into gjTo
            from tt in gjTo.DefaultIfEmpty()
            select new { m, p, pos, tf, tt };

        if (filter.CycleId.HasValue) q = q.Where(x => x.m.CycleId == filter.CycleId.Value);
        if (filter.ItemId.HasValue) q = q.Where(x => x.m.ItemId == filter.ItemId.Value);
        if (filter.PlayerId.HasValue) q = q.Where(x => x.m.PlayerId == filter.PlayerId.Value);

        if (!string.IsNullOrWhiteSpace(filter.PlayerName))
        {
            var pat = $"%{filter.PlayerName.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.p.Name, pat));
        }

        if (!string.IsNullOrWhiteSpace(filter.TeamName))
        {
            var pat = $"%{filter.TeamName.Trim()}%";
            q = q.Where(x =>
                (x.tf != null && EF.Functions.ILike(x.tf.TeamName, pat)) ||
                (x.tt != null && EF.Functions.ILike(x.tt.TeamName, pat)));
        }

        if (!string.IsNullOrWhiteSpace(filter.TargetTeamName))
        {
            var pat = $"%{filter.TargetTeamName.Trim()}%";
            q = q.Where(x => x.tt != null && EF.Functions.ILike(x.tt.TeamName, pat));
        }

        if (!string.IsNullOrWhiteSpace(filter.PerformedBy))
        {
            var pat = $"%{filter.PerformedBy.Trim()}%";
            q = q.Where(x => EF.Functions.ILike(x.m.PerformedBy, pat));
        }

        if (filter.Type.HasValue) q = q.Where(x => x.m.Type == filter.Type.Value);
        if (filter.FromUtc.HasValue) q = q.Where(x => x.m.CreatedAtUtc >= EnsureUtc(filter.FromUtc.Value));
        if (filter.ToUtc.HasValue) q = q.Where(x => x.m.CreatedAtUtc <= EnsureUtc(filter.ToUtc.Value));

        var total = await q.CountAsync(ct);
        var pageSize = Math.Min(filter.PageSize, MaxPageSize);
        var skip = (filter.Page - 1) * pageSize;

        var items = await q
            .OrderByDescending(x => x.m.CreatedAtUtc)  
            .Skip(skip)
            .Take(pageSize)
            .Select(x => new MarketTransactionDto(
                x.m.TransactionId,
                x.m.CycleId,
                x.m.ItemId,
                x.m.PlayerId,
                x.p.Name,
                x.pos.Name,
                x.m.TeamId,
                x.tf != null ? x.tf.TeamName : null,
                x.m.TargetTeamId,
                x.tt != null ? x.tt.TeamName : null,
                x.m.Type,
                x.m.Type.ToDisplayName(), 
                x.m.Amount,
                x.m.PerformedBy,
                x.m.Notes,
                x.m.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<MarketTransactionDto>(items, total);
    }


    public async Task<IReadOnlyList<MarketTransactionDto>> ExportAsync(MarketHistoryFilter filter, CancellationToken ct)
    {
        ValidateFilter(filter, allowZeroPage: true);

        var q =
            from m in _dbContext.MarketTransactions.AsNoTracking()
            join p in _dbContext.Players.AsNoTracking() on m.PlayerId equals p.PlayerId
            join pos in _dbContext.Positions.AsNoTracking() on p.PositionId equals pos.PositionId
            join tf0 in _dbContext.Teams.AsNoTracking() on m.TeamId equals tf0.TeamId into gjFrom
            from tf in gjFrom.DefaultIfEmpty()
            join tt0 in _dbContext.Teams.AsNoTracking() on m.TargetTeamId equals tt0.TeamId into gjTo
            from tt in gjTo.DefaultIfEmpty()
            select new { m, p, pos, tf, tt };


        var items = await q
            .OrderByDescending(x => x.m.CreatedAtUtc)     
            .Take(MaxExportRecords)
            .Select(x => new MarketTransactionDto(
                x.m.TransactionId,
                x.m.CycleId,
                x.m.ItemId,
                x.m.PlayerId,
                x.p.Name,
                x.pos.Name,
                x.m.TeamId,
                x.tf != null ? x.tf.TeamName : null,
                x.m.TargetTeamId,
                x.tt != null ? x.tt.TeamName : null,
                x.m.Type,
                x.m.Type.ToDisplayName(),                 
                x.m.Amount,
                x.m.PerformedBy,
                x.m.Notes,
                x.m.CreatedAtUtc))
            .ToListAsync(ct);

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

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
