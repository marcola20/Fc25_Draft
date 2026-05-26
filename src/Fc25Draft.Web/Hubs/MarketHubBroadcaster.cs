using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Fc25Draft.Web.Hubs;

public sealed class MarketHubBroadcaster : IMarketBroadcaster
{
    private readonly IHubContext<MarketHub> _hubContext;

    public MarketHubBroadcaster(IHubContext<MarketHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task BidUpdatedAsync(Guid cycleId, MarketItemVm item, CancellationToken ct)
        => _hubContext.Clients
            .Group(MarketHub.CycleGroup(cycleId))
            .SendAsync("BidUpdated", item, ct);

    public Task ItemBoughtAsync(Guid cycleId, MarketItemVm item, CancellationToken ct)
        => _hubContext.Clients
            .Group(MarketHub.CycleGroup(cycleId))
            .SendAsync("ItemBought", item, ct);

    public Task ItemClosedAsync(Guid cycleId, MarketItemVm item, CancellationToken ct)
        => _hubContext.Clients
            .Group(MarketHub.CycleGroup(cycleId))
            .SendAsync("ItemClosed", item, ct);
}
