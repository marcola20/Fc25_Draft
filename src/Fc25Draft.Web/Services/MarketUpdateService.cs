using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;

namespace Fc25Draft.Web.Services;

/// <summary>
/// Singleton — notifica todos os circuitos Blazor quando um item de mercado muda.
/// Mesmo padrão do LotteryStateService: evento C# in-process, sem SignalR.
/// </summary>
public sealed class MarketUpdateService : IMarketBroadcaster
{
    public event Action<Guid, MarketItemVm>? OnBidUpdated;
    public event Action<Guid, MarketItemVm>? OnItemBought;
    public event Action<Guid, MarketItemVm>? OnItemClosed;

    public Task BidUpdatedAsync(Guid cycleId, MarketItemVm item, CancellationToken ct)
    {
        OnBidUpdated?.Invoke(cycleId, item);
        return Task.CompletedTask;
    }

    public Task ItemBoughtAsync(Guid cycleId, MarketItemVm item, CancellationToken ct)
    {
        OnItemBought?.Invoke(cycleId, item);
        return Task.CompletedTask;
    }

    public Task ItemClosedAsync(Guid cycleId, MarketItemVm item, CancellationToken ct)
    {
        OnItemClosed?.Invoke(cycleId, item);
        return Task.CompletedTask;
    }
}
