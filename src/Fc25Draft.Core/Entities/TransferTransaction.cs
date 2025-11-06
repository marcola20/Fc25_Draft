namespace Fc25Draft.Core.Entities;

public class TransferTransaction
{
    public Guid TransferTransactionId { get; set; }
    public Guid OfferId { get; set; }
    public Guid FromTeamId { get; set; }
    public Guid ToTeamId { get; set; }
    public decimal? CashAmount { get; set; }
    public decimal? SellOnPercent { get; set; }
    public DateTime ExecutedAtUtc { get; set; }

    public TransferOffer Offer { get; set; } = null!;
    public Team FromTeam { get; set; } = null!;
    public Team ToTeam { get; set; } = null!;
    public ICollection<TransferTransactionPlayer> Players { get; set; } = new List<TransferTransactionPlayer>();
}
