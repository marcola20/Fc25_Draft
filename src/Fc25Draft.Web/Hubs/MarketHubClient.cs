using Fc25Draft.Core.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

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

    public Func<MarketItemVm, Task>? OnBidUpdated { get; set; }
    public Func<MarketItemVm, Task>? OnItemClosed { get; set; }
    public Func<MarketItemVm, Task>? OnItemBought { get; set; }

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

        _connection.On<MarketItemVm>("BidUpdated", async vm => await InvokeSafeAsync(OnBidUpdated, vm));
        _connection.On<MarketItemVm>("ItemClosed", async vm => await InvokeSafeAsync(OnItemClosed, vm));
        _connection.On<MarketItemVm>("ItemBought", async vm => await InvokeSafeAsync(OnItemBought, vm));

        await _connection.StartAsync(ct);
    }

    public async Task<bool> JoinCycle(Guid cycleId, CancellationToken ct = default)
    {
        if (cycleId == Guid.Empty || _connection is null)
        {
            return false;
        }

        try
        {
            return await _connection.InvokeAsync<bool>("JoinCycle", cycleId, ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join market cycle {CycleId}.", cycleId);
            return false;
        }
    }

    private async Task InvokeSafeAsync(Func<MarketItemVm, Task>? handler, MarketItemVm payload)
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
