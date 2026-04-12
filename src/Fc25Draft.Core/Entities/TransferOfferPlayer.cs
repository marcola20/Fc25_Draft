namespace Fc25Draft.Core.Entities;

public class TransferOfferPlayer
{
    public Guid OfferId { get; set; }
    public int PlayerId { get; set; }
    public bool IsTarget { get; set; }

    public TransferOffer Offer { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
