using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketService
{
    Task<MarketCycleDto> EnsureCycleAsync(CancellationToken ct);
    Task<List<MarketItemDto>> GetActiveItemsAsync(CancellationToken ct);
    Task<MarketItemDto?> GetItemAsync(Guid itemId, CancellationToken ct);
    Task<BidResultDto> PlaceBidAsync(Guid itemId, string teamToken, decimal amount, uint expectedRowVersion, CancellationToken ct);
    Task<BuyNowResultDto> BuyNowAsync(Guid itemId, string teamToken, uint expectedRowVersion, CancellationToken ct);
    Task<int> CloseExpiredItemsAsync(CancellationToken ct);
}
