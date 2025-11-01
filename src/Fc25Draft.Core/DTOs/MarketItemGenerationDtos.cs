namespace Fc25Draft.Core.DTOs;

public record MarketItemGenerationFilters(
    IReadOnlyCollection<int>? PlayerIds,
    IReadOnlyCollection<short>? PositionIds,
    int? MinOverall,
    int? MaxOverall,
    int? MinAge,
    int? MaxAge);

public record MarketItemLifecycleOptions(
    DateTime? PublishAtUtc,
    DateTime? ExpiresAtUtc,
    int? DurationHours);

public record MarketItemGenerationOptions(
    int DesiredCount,
    int? Seed,
    MarketItemGenerationFilters Filters,
    MarketItemLifecycleOptions LifecycleOptions);

public record MarketItemGenerationCandidate(
    int PlayerId,
    string PlayerName,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age);

public record MarketItemGenerationItem(
    int PlayerId,
    string PlayerName,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    decimal BasePrice,
    decimal? BuyNowPrice,
    decimal MinIncrement,
    DateTime ExpiresAtUtc);

public record MarketItemGenerationPreview(
    Guid CycleId,
    int RequestedCount,
    int EligibleCount,
    int Seed,
    IReadOnlyList<MarketItemGenerationItem> Items);

public record MarketItemGenerationResult(
    Guid CycleId,
    int RequestedCount,
    int EligibleCount,
    int Seed,
    int CreatedCount,
    int SkippedExistingCount,
    IReadOnlyList<MarketItemGenerationItem> Items);
