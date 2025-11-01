using System;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.MarketCycles;

public class GenerateItemsRequestDto
{
    [Range(1, 500, ErrorMessage = "Informe uma quantidade entre 1 e 500.")]
    public int? DesiredCount { get; set; }

    public List<short> PositionIds { get; set; } = new();

    [Range(0, 200, ErrorMessage = "Overall mínimo inválido.")]
    public int? MinOverall { get; set; }

    [Range(0, 200, ErrorMessage = "Overall máximo inválido.")]
    public int? MaxOverall { get; set; }

    [Range(1, 50, ErrorMessage = "Limite por time inválido.")]
    public int? MaxPerTeam { get; set; }

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
    int GeneratedCount,
    int SkippedCount,
    int Seed,
    DateTime? FirstExpiresAtUtc,
    DateTime? LastExpiresAtUtc,
    IReadOnlyList<MarketItemGenerationItemDto> Items);

public record MarketItemGenerationResultDto(
    int RequestedCount,
    int EligibleCount,
    int GeneratedCount,
    int SkippedCount,
    int Seed,
    DateTime? FirstExpiresAtUtc,
    DateTime? LastExpiresAtUtc,
    IReadOnlyList<MarketItemGenerationItemDto> Items);

public record MarketItemGenerationItemDto(
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

public record MarketItemGenerationDeleteResultDto(int RemovedCount);
