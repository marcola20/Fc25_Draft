using System;

namespace Fc25Draft.Core.DTOs;

public sealed record MarketCycleStatusUpdateResult
{
    public MarketCycleStatusUpdateResult(MarketCycleDto cycle, MarketCycleSettlementSummary? settlementSummary)
    {
        Cycle = cycle ?? throw new ArgumentNullException(nameof(cycle));
        SettlementSummary = settlementSummary;
    }

    public MarketCycleDto Cycle { get; }

    public MarketCycleSettlementSummary? SettlementSummary { get; }
}

public sealed record MarketCycleSettlementSummary(int Sold, int Expired, int Canceled)
{
    public int Total => Sold + Expired + Canceled;
}
