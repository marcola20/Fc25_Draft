using DocumentFormat.OpenXml.InkML;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.SignalR;

namespace Fc25Draft.Web.Hubs
{
    public class MarketHub : Hub
    {
        public static string CycleGroup(Guid cycleId) => $"cycle:{cycleId:N}";

        public async Task JoinCycle(Guid cycleId, CancellationToken ct = default)
        {
            if (cycleId == Guid.Empty) return;
            await Groups.AddToGroupAsync(Context.ConnectionId, CycleGroup(cycleId), ct);
        }

        public override Task OnConnectedAsync()
        {
            // opcional: logar/telemetria
            return base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception)
        {
            // opcional
            return base.OnDisconnectedAsync(exception);
        }
    }
}
