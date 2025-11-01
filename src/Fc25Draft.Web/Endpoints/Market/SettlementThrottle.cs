using System;

namespace Fc25Draft.Web.Endpoints.Market;

internal static class SettlementThrottle
{
    private static readonly object Sync = new();
    private static DateTime _lastRunUtc = DateTime.MinValue;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    public static bool TryAcquire()
    {
        var now = DateTime.UtcNow;
        lock (Sync)
        {
            if (now - _lastRunUtc <= Interval)
            {
                return false;
            }

            _lastRunUtc = now;
            return true;
        }
    }
}
