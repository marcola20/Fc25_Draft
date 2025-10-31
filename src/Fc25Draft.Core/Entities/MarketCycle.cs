namespace Fc25Draft.Core.Entities;

public class MarketCycle
{
    public Guid CycleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public MarketCycleStatus Status { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public ICollection<MarketItem> Items { get; set; } = new List<MarketItem>();
}
