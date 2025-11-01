using System.Globalization;
using Fc25Draft.Core.Utilities;
using Xunit;

namespace Fc25Draft.Tests;

public class MarketPricingTests
{
    [Fact]
    public void ComputeRequiredMinBid_NoBids_ReturnsBasePlusIncrement()
    {
        var required = MarketPricing.ComputeRequiredMinBid(100m, 10m, null, null);

        Assert.Equal(110m, required);
    }

    [Fact]
    public void ComputeRequiredMinBid_WithCurrentLeader_UsesLeaderPlusIncrement()
    {
        var required = MarketPricing.ComputeRequiredMinBid(100m, 5m, 150m, 1000m);

        Assert.Equal(155m, required);
    }

    [Fact]
    public void ComputeRequiredMinBid_RespectsBuyNowCap()
    {
        var required = MarketPricing.ComputeRequiredMinBid(100m, 25m, 940m, 950m);

        Assert.Equal(950m, required);
    }

    [Fact]
    public void ComputeRequiredMinBid_HandlesBrazilianValues()
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var basePrice = decimal.Parse("28.684.000,00", culture);
        var minIncrement = decimal.Parse("1.000.000,00", culture);
        var buyNow = decimal.Parse("35.000.000,00", culture);

        var required = MarketPricing.ComputeRequiredMinBid(basePrice, minIncrement, null, buyNow);

        Assert.Equal(29_684_000m, required);
    }

    [Fact]
    public void ComputeRequiredMinBid_RoundsAwayFromZero()
    {
        var required = MarketPricing.ComputeRequiredMinBid(100.01m, 0.015m, null, null);

        Assert.Equal(100.02m, required);
    }
}
