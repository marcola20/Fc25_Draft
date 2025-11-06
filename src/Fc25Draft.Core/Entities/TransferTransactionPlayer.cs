namespace Fc25Draft.Core.Entities;

public class TransferTransactionPlayer
{
    public Guid TransferTransactionPlayerId { get; set; }
    public Guid TransferTransactionId { get; set; }
    public int PlayerId { get; set; }

    public TransferTransaction TransferTransaction { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
