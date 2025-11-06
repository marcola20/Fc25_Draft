namespace Fc25Draft.Core.Entities;

public class PlayerSellOnClause
{
    public Guid PlayerSellOnClauseId { get; set; }
    public int PlayerId { get; set; }
    public Guid BeneficiaryTeamId { get; set; }
    public Guid? TransferTransactionId { get; set; }
    public decimal Percentage { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    public Player Player { get; set; } = null!;
    public Team BeneficiaryTeam { get; set; } = null!;
    public TransferTransaction? TransferTransaction { get; set; }
}
