namespace Fc25Draft.Core.Entities;

public class TransferOffer
{
    public Guid OfferId { get; set; }
    public Guid FromTeamId { get; set; }
    public Guid ToTeamId { get; set; }
    public OfferType Type { get; set; }
    public OfferStatus Status { get; set; }
    public decimal Money { get; set; }
    public Guid? MoneyPayerTeamId { get; set; }
    public decimal SellOnPercentage { get; set; }
    public string? Clauses { get; set; }
    public string? Notes { get; set; }
    public Guid? ParentOfferId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Team FromTeam { get; set; } = null!;
    public Team ToTeam { get; set; } = null!;
    public TransferOffer? ParentOffer { get; set; }
    public ICollection<TransferOffer> CounterOffers { get; set; } = new List<TransferOffer>();
    public ICollection<TransferOfferPlayer> Players { get; set; } = new List<TransferOfferPlayer>();
}
