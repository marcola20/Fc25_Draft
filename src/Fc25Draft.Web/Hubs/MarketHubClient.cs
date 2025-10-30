using System;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Web.Hubs;

public class MarketHubClient : IAsyncDisposable
{
    private readonly NavigationManager _navigation;
    private readonly ILogger<MarketHubClient> _logger;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private HubConnection? _connection;

    public event Func<MarketItemVm, Task>? OnBidUpdated;
    public event Func<MarketItemVm, Task>? OnItemBought;
    public event Func<MarketItemVm, Task>? OnItemClosed;

    public MarketHubClient(NavigationManager navigation, ILogger<MarketHubClient> logger)
    {
        _navigation = navigation;
        _logger = logger;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        await _connectionLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_connection is null)
            {
                _connection = new HubConnectionBuilder()
                    .WithUrl(_navigation.ToAbsoluteUri("/hubs/market"))
                    .WithAutomaticReconnect()
                    .Build();

                RegisterHandlers(_connection);
            }

            if (_connection.State == HubConnectionState.Connected ||
                _connection.State == HubConnectionState.Connecting)
            {
                return;
            }

            await _connection.StartAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao conectar ao hub do mercado.");
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public Task JoinCycle(Guid cycleId, CancellationToken ct = default)
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("A conexão com o hub não foi inicializada. Chame ConnectAsync primeiro.");
        }

        return _connection.InvokeAsync("JoinCycle", cycleId, ct);
    }

    private void RegisterHandlers(HubConnection connection)
    {
        connection.On<MarketItemVm>("BidUpdated", item => DispatchAsync(OnBidUpdated, item));
        connection.On<MarketItemVm>("ItemBought", item => DispatchAsync(OnItemBought, item));
        connection.On<MarketItemVm>("ItemClosed", item => DispatchAsync(OnItemClosed, item));
    }

    private static Task DispatchAsync(Func<MarketItemVm, Task>? handler, MarketItemVm item)
        => handler is null ? Task.CompletedTask : handler.Invoke(item);

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            try
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao descartar a conexão do hub do mercado.");
            }
        }

        _connectionLock.Dispose();
    }
}
