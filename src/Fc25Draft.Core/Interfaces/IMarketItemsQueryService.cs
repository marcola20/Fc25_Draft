using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketItemsQueryService
{
    Task<PagedResult<MarketItemListDto>> QueryAsync(MarketItemsQuery query, CancellationToken ct);
}
