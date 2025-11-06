namespace Fc25Draft.Core.Entities;

public class TransferOfferTarget
{
    public Guid OfferTargetId { get; set; }
    public Guid OfferId { get; set; }
    public int PlayerId { get; set; }

    public TransferOffer Offer { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
