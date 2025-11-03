namespace Fc25Draft.Core.Entities;

public class TransferOfferSwapPlayer
{
    public Guid SwapPlayerId { get; set; }
    public Guid OfferId { get; set; }
    public int PlayerId { get; set; }
    public Guid TeamId { get; set; }

    public TransferOffer Offer { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
