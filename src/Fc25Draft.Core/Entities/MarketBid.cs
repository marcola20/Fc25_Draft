namespace Fc25Draft.Core.Entities;

public class MarketBid
{
    public Guid BidId { get; set; }
    public Guid ItemId { get; set; }
    public Guid TeamId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public MarketItem Item { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
