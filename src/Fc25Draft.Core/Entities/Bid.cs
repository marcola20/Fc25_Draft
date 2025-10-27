namespace Fc25Draft.Core.Entities;

public class Bid
{
    public Guid BidId { get; set; }

    public Guid MarketItemId { get; set; }

    public Guid TeamId { get; set; }

    public decimal Valor { get; set; }

    public DateTime DataUtc { get; set; }

    public TransferMarketItem MarketItem { get; set; } = null!;

    public Team Team { get; set; } = null!;
}
