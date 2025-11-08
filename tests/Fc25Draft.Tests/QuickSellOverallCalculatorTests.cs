using System;
using System.Collections.Generic;
using Fc25Draft.Infra.Services;
using Xunit;

namespace Fc25Draft.Tests;

public class QuickSellOverallCalculatorTests
{
    [Theory]
    [InlineData(77, 5, 6)]
    [InlineData(79, 5, 6)]
    [InlineData(80, 3, 5)]
    [InlineData(82, 3, 5)]
    [InlineData(83, 2, 3)]
    [InlineData(84, 2, 3)]
    public void CalculateDelta_RandomRangesRespectConfiguredBounds(int overall, int expectedMin, int expectedMax)
    {
        var calls = new List<(int Min, int Max)>();

        int Random(int min, int max)
        {
            calls.Add((min, max));
            return max;
        }

        var delta = QuickSellOverallCalculator.CalculateDelta(overall, Random);

        var call = Assert.Single(calls);
        Assert.Equal(expectedMin, call.Min);
        Assert.Equal(expectedMax, call.Max);
        Assert.Equal(expectedMax, delta);
    }

    [Theory]
    [InlineData(77, 82)]
    [InlineData(80, 85)]
    [InlineData(83, 85)]
    [InlineData(84, 86)]
    [InlineData(85, 86)]
    [InlineData(86, 87)]
    [InlineData(87, 87)]
    [InlineData(98, 98)]
    [InlineData(99, 99)]
    public void CalculateNewOverall_RespectsBoundaries(int currentOverall, int expectedMax)
    {
        int CounterRandom(int min, int max) => max;

        var newOverall = QuickSellOverallCalculator.CalculateNewOverall(currentOverall, CounterRandom);

        Assert.InRange(newOverall, currentOverall, expectedMax);
    }

    [Fact]
    public void CalculateNewOverall_DoesNotExceedNinetyNine()
    {
        int ExaggeratedRandom(int min, int max) => max + 20;

        var newOverall = QuickSellOverallCalculator.CalculateNewOverall(84, ExaggeratedRandom);

        Assert.Equal(99, newOverall);
    }

    [Fact]
    public void CalculateDelta_ReturnsZeroForBelowThreshold()
    {
        int RandomMax(int min, int max) => max;

        var delta = QuickSellOverallCalculator.CalculateDelta(70, RandomMax);

        Assert.Equal(0, delta);
    }

    [Fact]
    public void CalculateDelta_ReturnsZeroForEightySevenOrAbove()
    {
        int RandomMax(int min, int max) => max;

        var delta = QuickSellOverallCalculator.CalculateDelta(90, RandomMax);

        Assert.Equal(0, delta);
    }
}
