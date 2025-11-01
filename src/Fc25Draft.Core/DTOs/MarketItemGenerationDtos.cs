namespace Fc25Draft.Core.DTOs;

public record MarketItemGenerationFilters(
    IReadOnlyCollection<short>? PositionIds,
    int? MinOverall,
    int? MaxOverall);

public record MarketItemExpirationOptions(
    bool AutoSpreadAcrossCycle,
    TimeSpan? MinItemLifespan,
    TimeSpan? MaxItemLifespan);

public record MarketItemGenerationOptions(
    int? DesiredCount,
    int? Seed,
    MarketItemGenerationFilters Filters,
    int? MaxPerTeam,
    bool ExcludeAlreadyListedInOpenCycles,
    bool EnsureUniquePlayerPerCycle,
    MarketItemExpirationOptions ExpirationOptions);

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
    int GeneratedCount,
    int SkippedCount,
    int Seed,
    DateTime? FirstExpiresAtUtc,
    DateTime? LastExpiresAtUtc,
    IReadOnlyList<MarketItemGenerationItem> Items);

public record MarketItemGenerationResult(
    Guid CycleId,
    int RequestedCount,
    int EligibleCount,
    int GeneratedCount,
    int SkippedCount,
    int Seed,
    DateTime? FirstExpiresAtUtc,
    DateTime? LastExpiresAtUtc,
    IReadOnlyList<MarketItemGenerationItem> Items);
