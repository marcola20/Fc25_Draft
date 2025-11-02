using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.MarketCycles;

public class MarketItemGenerationRequestDto
{
    [Range(1, 500)]
    public int DesiredCount { get; set; } = 1;

    public List<short> PositionIds { get; set; } = new();

    [Range(0, 200)]
    public int? MinOverall { get; set; }

    [Range(0, 200)]
    public int? MaxOverall { get; set; }

    [Range(10, 60)]
    public int? MinAge { get; set; }

    [Range(10, 60)]
    public int? MaxAge { get; set; }

    [Range(0, 50)]
    public int? MaxPerTeam { get; set; }

    [Range(0, 50)]
    public int? MaxPerPosition { get; set; }

    public bool ExcludeAlreadyListedInOpenCycles { get; set; } = true;

    public bool EnsureUniquePlayerPerCycle { get; set; } = true;

    public int? Seed { get; set; }

    public TimeSpan? MinItemLifespan { get; set; }

    public TimeSpan? MaxItemLifespan { get; set; }

    public bool AutoSpreadExpirationsAcrossCycle { get; set; } = true;
}

public record MarketItemGenerationPreviewDto(
    int RequestedCount,
    int EligibleCount,
    int Seed,
    IReadOnlyList<MarketItemGenerationItemDto> Items,
    IReadOnlyList<MarketItemGenerationSkipDto> Skipped,
    DateTime? FirstExpirationUtc,
    DateTime? LastExpirationUtc);

public record MarketItemGenerationResultDto(
    int RequestedCount,
    int EligibleCount,
    int Seed,
    int CreatedCount,
    IReadOnlyList<MarketItemGenerationItemDto> Items,
    IReadOnlyList<MarketItemGenerationSkipDto> Skipped,
    DateTime? FirstExpirationUtc,
    DateTime? LastExpirationUtc);

public record MarketItemGenerationItemDto(
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

public record MarketItemGenerationSkipDto(int PlayerId, string PlayerName, string Reason);

public record MarketItemGenerationDeleteResultDto(int RemovedCount);
