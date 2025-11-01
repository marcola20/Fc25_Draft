using System;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Infra.Services;

public class MarketItemsQueryService : IMarketItemsQueryService
{
    private const int MaxPageSize = 200;
    private readonly DraftDbContext _dbContext;

    public MarketItemsQueryService(DraftDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PagedResult<MarketItemListDto>> QueryAsync(MarketItemsQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedPage = query.Page < 1 ? 1 : query.Page;
        var normalizedPageSize = query.PageSize < 1 ? 20 : Math.Min(query.PageSize, MaxPageSize);

        var itemsQuery = _dbContext.MarketItems
            .AsNoTracking()
            .Where(i => i.CycleId == query.CycleId)
            .Where(i => i.Cycle.Status != MarketCycleStatus.Draft)
            .Include(i => i.Player)
                .ThenInclude(p => p.Position)
            .Include(i => i.CurrentLeaderTeam)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            itemsQuery = itemsQuery.Where(i => EF.Functions.ILike(i.Player.Name, pattern));
        }

        if (query.PositionIds.Count > 0)
        {
            itemsQuery = itemsQuery.Where(i => query.PositionIds.Contains(i.Player.PositionId));
        }

        if (query.OverallMin.HasValue)
        {
            itemsQuery = itemsQuery.Where(i => i.Player.Overall >= query.OverallMin.Value);
        }

        if (query.OverallMax.HasValue)
        {
            itemsQuery = itemsQuery.Where(i => i.Player.Overall <= query.OverallMax.Value);
        }

        if (query.Statuses.Count > 0)
        {
            itemsQuery = itemsQuery.Where(i => query.Statuses.Contains(i.Status));
        }

        var total = await itemsQuery.CountAsync(ct).ConfigureAwait(false);

        IQueryable<MarketItem> orderedQuery;

        if (query.SortBy == MarketItemsSortField.ExpiresAtUtc && !query.SortDescending)
        {
            var nowUtc = DateTime.UtcNow;

            orderedQuery = itemsQuery
                .Select(i => new
                {
                    Item = i,
                    StatusPriority = i.ExpiresAtUtc <= nowUtc
                        ? 0
                        : i.Status == MarketItemStatus.Active ? 3
                        : i.Status == MarketItemStatus.Draft ? 2
                        : i.Status == MarketItemStatus.Canceled ? 1
                        : 0
                })
                .OrderByDescending(x => x.StatusPriority)
                .ThenBy(x => x.Item.ExpiresAtUtc)
                .ThenByDescending(x => x.Item.CreatedAtUtc)
                .ThenBy(x => x.Item.ItemId)
                .Select(x => x.Item);
        }
        else
        {
            orderedQuery = query.SortBy switch
            {
                MarketItemsSortField.CurrentBid when query.SortDescending => itemsQuery
                    .OrderByDescending(i => i.CurrentLeaderAmount ?? i.BasePrice)
                    .ThenBy(i => i.ExpiresAtUtc)
                    .ThenBy(i => i.ItemId),
                MarketItemsSortField.CurrentBid => itemsQuery
                    .OrderBy(i => i.CurrentLeaderAmount ?? i.BasePrice)
                    .ThenBy(i => i.ExpiresAtUtc)
                    .ThenBy(i => i.ItemId),
                _ when query.SortDescending => itemsQuery
                    .OrderByDescending(i => i.ExpiresAtUtc)
                    .ThenBy(i => i.ItemId),
                _ => itemsQuery
                    .OrderBy(i => i.ExpiresAtUtc)
                    .ThenBy(i => i.ItemId)
            };
        }

        var skip = (normalizedPage - 1) * normalizedPageSize;

        var results = await orderedQuery
            .Skip(skip)
            .Take(normalizedPageSize)
            .Select(i => new MarketItemListDto(
                i.ItemId,
                i.CycleId,
                i.PlayerId,
                i.Player.Name,
                i.Player.Position.Name,
                i.Player.Overall,
                i.Player.Age ?? 0,
                i.BasePrice,
                i.CurrentLeaderAmount,
                i.BuyNowPrice,
                i.MinIncrement,
                MarketPricing.ComputeRequiredMinBid(i.BasePrice, i.MinIncrement, i.CurrentLeaderAmount, i.BuyNowPrice),
                i.ExpiresAtUtc,
                i.Status,
                i.Status.ToDisplayName(),
                i.CurrentLeaderTeamId,
                i.CurrentLeaderTeam != null && !string.IsNullOrWhiteSpace(i.CurrentLeaderTeam.TeamName)
                    ? i.CurrentLeaderTeam.TeamName
                    : i.CurrentLeaderTeamId.HasValue ? i.CurrentLeaderTeamId.Value.ToString() : null,
                i.RowVersion))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<MarketItemListDto>(results, total, normalizedPage, normalizedPageSize);
    }
}
