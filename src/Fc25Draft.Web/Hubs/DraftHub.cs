using Microsoft.AspNetCore.SignalR;

namespace Fc25Draft.Web.Hubs;

// Server-push only hub: the server broadcasts via IHubContext<DraftHub>.
// Clients connect and subscribe to "DraftAtualizado" — no client-to-server
// methods are needed, so this class intentionally has no members.
public class DraftHub : Hub
{
}
