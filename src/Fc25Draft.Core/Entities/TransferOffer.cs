namespace Fc25Draft.Core.Entities;

public class TransferOffer
{
    public Guid OfferId { get; set; }
    public Guid FromTeamId { get; set; }
    public Guid ToTeamId { get; set; }
    public int PlayerId { get; set; }
    public decimal? OfferedFee { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
    public TransferOfferStatus Status { get; set; }
    public string? Message { get; set; }
    public string? ResponseMessage { get; set; }
    public uint RowVersion { get; set; }

    public Team FromTeam { get; set; } = null!;
    public Team ToTeam { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public ICollection<TransferOfferSwapPlayer> SwapPlayers { get; set; } = new List<TransferOfferSwapPlayer>();
}
