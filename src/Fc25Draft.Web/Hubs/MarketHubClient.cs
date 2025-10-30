using System.Threading;
using System.Threading.Tasks;

namespace Fc25Draft.Web.Hubs;

public class MarketHubClient
{
    public virtual Task StartAsync(CancellationToken ct = default)
    {
        return Task.CompletedTask;
    }
}
