using Fc25Draft.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Web.Hubs;

public class MarketHub : Hub
{
    private readonly IMarketCycleService _cycleService;
    private readonly ILogger<MarketHub> _logger;

    public MarketHub(IMarketCycleService cycleService, ILogger<MarketHub> logger)
    {
        _cycleService = cycleService;
        _logger = logger;
    }

    public static string CycleGroup(Guid cycleId) => $"market-cycle:{cycleId:D}";

    public async Task<bool> JoinCycle(Guid cycleId, CancellationToken ct = default)
    {
        if (cycleId == Guid.Empty)
        {
            _logger.LogWarning("Connection {ConnectionId} attempted to join market cycle with empty id.", Context.ConnectionId);
            return false;
        }

        try
        {
            var cycle = await _cycleService.ResolveAsync(cycleId, ct).ConfigureAwait(false);
            if (cycle is null)
            {
                _logger.LogWarning("Connection {ConnectionId} attempted to join missing market cycle {CycleId}.", Context.ConnectionId, cycleId);
                return false;
            }

            var groupName = CycleGroup(cycleId);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName, ct).ConfigureAwait(false);

            _logger.LogInformation("Connection {ConnectionId} joined market cycle {CycleId} (group: {GroupName}).", Context.ConnectionId, cycleId, groupName);
            return true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Join cycle canceled for connection {ConnectionId} and cycle {CycleId}.", Context.ConnectionId, cycleId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add connection {ConnectionId} to market cycle {CycleId}.", Context.ConnectionId, cycleId);
            return false;
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Connection {ConnectionId} connected to market hub.", Context.ConnectionId);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogInformation(exception, "Connection {ConnectionId} disconnected from market hub with error.", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Connection {ConnectionId} disconnected from market hub.", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }
}
