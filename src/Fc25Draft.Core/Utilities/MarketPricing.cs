using System;

namespace Fc25Draft.Core.Utilities;

public static class MarketPricing
{
    public static decimal ComputeRequiredMinBid(
        decimal basePrice,
        decimal minIncrement,
        decimal? currentLeaderAmount,
        decimal? buyNowPrice)
    {
        if (minIncrement < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(minIncrement), "Min increment must be non-negative.");
        }

        var referenceAmount = currentLeaderAmount ?? basePrice;
        var incremented = referenceAmount + minIncrement;
        var required = Math.Max(basePrice, incremented);

        if (buyNowPrice.HasValue)
        {
            required = Math.Min(required, buyNowPrice.Value);
        }

        return decimal.Round(required, 2, MidpointRounding.AwayFromZero);
    }
}
