namespace Fc25Draft.Core.DTOs;

public record PricingConfigDto(
    decimal BaseScale,
    decimal OverallBase,
    int OverallPivot,
    decimal BuyNowFactor,
    decimal MinIncrementRate,
    decimal MinIncrementStep,
    decimal AgeFactorUpTo22,
    decimal AgeFactor23To24,
    decimal AgeFactor25To26,
    decimal AgeFactor27To28,
    decimal AgeFactor29To30,
    decimal AgeFactor31To32,
    decimal AgeFactor33To34,
    decimal AgeFactor35Plus);
