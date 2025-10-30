using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using CoreItemVm = Fc25Draft.Core.DTOs.MarketItemVm;

namespace Fc25Draft.Web.Hubs;

public class MarketHubClient : IAsyncDisposable
{
    private readonly NavigationManager _navigationManager;
    private readonly ILogger<MarketHubClient> _logger;
    private HubConnection? _connection;

    public MarketHubClient(NavigationManager navigationManager, ILogger<MarketHubClient> logger)
    {
        _navigationManager = navigationManager;
        _logger = logger;
    }

    public Func<CoreItemVm, Task>? OnBidUpdated { get; set; }
    public Func<CoreItemVm, Task>? OnItemClosed { get; set; }
    public Func<CoreItemVm, Task>? OnItemBought { get; set; }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                await _connection.StartAsync(ct);
            }

            return;
        }

        var hubUri = _navigationManager.ToAbsoluteUri("/hubs/market");
        _connection = new HubConnectionBuilder()
            .WithUrl(hubUri)
            .WithAutomaticReconnect()
            .Build();

        _connection.On<CoreItemVm>("BidUpdated", async vm => await InvokeSafeAsync(OnBidUpdated, vm));
        _connection.On<CoreItemVm>("ItemClosed", async vm => await InvokeSafeAsync(OnItemClosed, vm));
        _connection.On<CoreItemVm>("ItemBought", async vm => await InvokeSafeAsync(OnItemBought, vm));

        await _connection.StartAsync(ct);
    }

    public async Task JoinCycle(Guid cycleId, CancellationToken ct = default)
    {
        if (cycleId == Guid.Empty || _connection is null)
        {
            return;
        }

        await _connection.InvokeAsync("JoinCycle", cycleId, ct);
    }

    private async Task InvokeSafeAsync(Func<CoreItemVm, Task>? handler, CoreItemVm payload)
    {
        if (handler is null)
        {
            return;
        }

        try
        {
            await handler(payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while handling market hub message.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is null)
        {
            return;
        }

        try
        {
            await _connection.DisposeAsync();
        }
        finally
        {
            _connection = null;
        }
    }
}
