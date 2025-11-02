using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public record MarketItemGenerationOptions(
    int DesiredCount,
    IReadOnlyCollection<short>? PositionIds,
    int? MinOverall,
    int? MaxOverall,
    int? MinAge,
    int? MaxAge,
    int? MaxPerTeam,
    int? MaxPerPosition,
    bool ExcludeAlreadyListedInOpenCycles,
    bool EnsureUniquePlayerPerCycle,
    int? Seed,
    TimeSpan? MinItemLifespan,
    TimeSpan? MaxItemLifespan,
    bool AutoSpreadExpirationsAcrossCycle);

public record MarketItemGenerationCandidate(
    int PlayerId,
    string PlayerName,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    Guid? TeamId,
    string? TeamName);

public record MarketItemGenerationItem(
    int PlayerId,
    string PlayerName,
    short PositionId,
    string PositionName,
    int Overall,
    int? Age,
    Guid? TeamId,
    string? TeamName,
    decimal BasePrice,
    decimal? BuyNowPrice,
    decimal MinIncrement,
    DateTime ExpiresAtUtc);

public record MarketItemGenerationSkip(
    int PlayerId,
    string PlayerName,
    string Reason);

public record MarketItemGenerationPreview(
    Guid CycleId,
    int RequestedCount,
    int EligibleCount,
    int Seed,
    IReadOnlyList<MarketItemGenerationItem> Items,
    IReadOnlyList<MarketItemGenerationSkip> Skipped,
    DateTime? FirstExpirationUtc,
    DateTime? LastExpirationUtc);

public record MarketItemGenerationResult(
    Guid CycleId,
    int RequestedCount,
    int EligibleCount,
    int Seed,
    int CreatedCount,
    IReadOnlyList<MarketItemGenerationItem> Items,
    IReadOnlyList<MarketItemGenerationSkip> Skipped,
    DateTime? FirstExpirationUtc,
    DateTime? LastExpirationUtc);
