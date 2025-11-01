namespace Fc25Draft.Core.Options;

public class MarketOptions
{
    public const string SectionName = "Market";

    public int CycleDurationHours { get; set; } = 48;
    public decimal BuyNowFactor { get; set; } = 1.80m;
    public decimal MinIncrementRate { get; set; } = 0.03m;
    public decimal MinIncrementStep { get; set; } = 500m;
    public decimal MarketVariancePct { get; set; } = 0.07m;
    public MarketBandsOptions Bands { get; set; } = new();
    public int OvrBandA_Min { get; set; }
    public int OvrBandA_Max { get; set; }
    public int OvrBandB_Min { get; set; }
    public int OvrBandB_Max { get; set; }
    public int OvrBandC_Min { get; set; }
    public int OvrBandC_Max { get; set; }
    public decimal BaseScale { get; set; } = 10_000_000m;
}

public class MarketBandsOptions
{
    public int CountTotal { get; set; } = 8;
    public int BandA_Count { get; set; } = 6;
    public int BandB_Count { get; set; } = 1;
    public int BandC_Count { get; set; } = 1;
}
