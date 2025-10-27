using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketTransactionService
{
    Task<TransferMarketItem> PlaceBidAsync(Guid marketItemId, Guid teamId, decimal bidValue, CancellationToken ct);
    Task<TransferMarketItem> BuyNowAsync(Guid marketItemId, Guid teamId, CancellationToken ct);
}
