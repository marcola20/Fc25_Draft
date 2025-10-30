namespace Fc25Draft.Core.Entities;

public class MarketItem
{
    public Guid ItemId { get; set; }
    public Guid CycleId { get; set; }
    public int PlayerId { get; set; }
    public decimal BasePrice { get; set; }
    public decimal? BuyNowPrice { get; set; }
    public decimal MinIncrement { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public MarketItemStatus Status { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastUpdateUtc { get; set; }
    public Guid? CurrentLeaderTeamId { get; set; }
    public decimal? CurrentLeaderAmount { get; set; }
    public Guid? WinnerTeamId { get; set; }
    public uint RowVersion { get; set; }

    public MarketCycle Cycle { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public Team? CurrentLeaderTeam { get; set; }
    public Team? WinnerTeam { get; set; }
    public ICollection<MarketBid> Bids { get; set; } = new List<MarketBid>();
}
