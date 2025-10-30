using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Web.Models.Market;

namespace Fc25Draft.Web.Services;

public class MarketClient
{
    public virtual Task<IReadOnlyList<MarketItemVm>> GetItemsAsync(MarketQueryVm query, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<MarketItemVm>>(Array.Empty<MarketItemVm>());
    }

    public virtual Task PlaceBidAsync(Guid itemId, BidRequest request, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }

    public virtual Task BuyNowAsync(Guid itemId, BuyNowRequest request, CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
