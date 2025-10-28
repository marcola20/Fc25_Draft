namespace Fc25Draft.Core.Entities;

public class MarketCycle
{
    public Guid CycleId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime NextCycleAtUtc { get; set; }
    public MarketCycleStatus Status { get; set; }

    public ICollection<MarketItem> Items { get; set; } = new List<MarketItem>();
}
