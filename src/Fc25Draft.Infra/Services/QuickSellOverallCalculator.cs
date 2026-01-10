using System;

namespace Fc25Draft.Infra.Services;

internal static class QuickSellOverallCalculator
{
    public static int CalculateNewOverall(int currentOverall)
        => CalculateNewOverall(currentOverall, RandomInclusive);

    public static int CalculateNewOverall(int currentOverall, Func<int, int, int> randomInclusive)
    {
        var delta = CalculateDelta(currentOverall, randomInclusive);
        return Math.Min(99, currentOverall + delta);
    }

    public static int CalculateDelta(int currentOverall, Func<int, int, int> randomInclusive)
    {
        if (randomInclusive is null)
            throw new ArgumentNullException(nameof(randomInclusive));

        if (currentOverall  >= 90)
            return 0;

        if (currentOverall is >= 89 and < 90)
            return randomInclusive(1, 1);

        if (currentOverall is >= 87 and <= 88)
            return randomInclusive(1, 2);

        if (currentOverall is >= 85 and <= 86)
            return randomInclusive(2, 3);

        if (currentOverall is >= 83 and <= 84)
            return randomInclusive(3, 4);

        if (currentOverall is >= 81 and <= 82)
            return randomInclusive(4, 5);

        if (currentOverall is 80)
            return randomInclusive(5, 6);

        if (currentOverall is >= 77 and <= 79)
            return randomInclusive(7, 8);

        return 0;
    }

    public static int RandomInclusive(int min, int max)
    {
        if (min > max)
            throw new ArgumentOutOfRangeException(nameof(min), "min cannot be greater than max");

        return Random.Shared.Next(min, max + 1);
    }
}
