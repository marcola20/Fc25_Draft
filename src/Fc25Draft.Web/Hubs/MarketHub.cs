using System;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Web.Hubs;

public class MarketHub : Hub
{
    private readonly ILogger<MarketHub> _logger;

    public MarketHub(ILogger<MarketHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Connection {ConnectionId} connected to the market hub.", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogWarning(exception, "Connection {ConnectionId} disconnected from the market hub with an error.", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Connection {ConnectionId} disconnected from the market hub.", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinCycle(Guid cycleId)
    {
        if (cycleId == Guid.Empty)
        {
            _logger.LogWarning("Connection {ConnectionId} attempted to join the market hub with an empty cycle id.", Context.ConnectionId);
            return;
        }

        var groupName = GetCycleGroup(cycleId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName, Context.ConnectionAborted);
        _logger.LogInformation("Connection {ConnectionId} joined market cycle {CycleId}.", Context.ConnectionId, cycleId);
    }

    private static string GetCycleGroup(Guid cycleId) => $"cycle:{cycleId:N}";
}
