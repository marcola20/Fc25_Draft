using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.MarketCycles;

public class MarketItemGenerationRequestDto
{
    [Range(1, 100)]
    public int DesiredCount { get; set; } = 1;

    public int? Seed { get; set; }

    [Required]
    public MarketItemGenerationFiltersDto Filters { get; set; } = new();

    [Required]
    public MarketItemGenerationLifecycleDto Lifecycle { get; set; } = new();
}

public class MarketItemGenerationFiltersDto
{
    public List<int> PlayerIds { get; set; } = new();

    public List<short> PositionIds { get; set; } = new();

    [Range(0, 200)]
    public int? MinOverall { get; set; }
    [Range(0, 200)]
    public int? MaxOverall { get; set; }
    [Range(15, 60)]
    public int? MinAge { get; set; }
    [Range(15, 60)]
    public int? MaxAge { get; set; }
}

public class MarketItemGenerationLifecycleDto
{
    public DateTime? PublishAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    [Range(1, 720)]
    public int? DurationHours { get; set; }
}

public record MarketItemGenerationPreviewDto(
    int RequestedCount,
    int EligibleCount,
    int Seed,
    IReadOnlyList<MarketItemGenerationItemDto> Items);

public record MarketItemGenerationResultDto(
    int RequestedCount,
    int EligibleCount,
    int Seed,
    int CreatedCount,
    int SkippedExistingCount,
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
