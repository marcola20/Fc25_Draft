using Fc25Draft.Core.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketBroadcaster
{
    Task BidUpdatedAsync(Guid cycleId, MarketItemVm item, CancellationToken ct);
    Task ItemBoughtAsync(Guid cycleId, MarketItemVm item, CancellationToken ct);
    Task ItemClosedAsync(Guid cycleId, MarketItemVm item, CancellationToken ct);
}
