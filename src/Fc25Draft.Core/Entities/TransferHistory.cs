namespace Fc25Draft.Core.Entities;

public class TransferHistory
{
    public Guid TransferId { get; set; }
    public TransferType Type { get; set; }
    public int PlayerId { get; set; }
    public Guid? FromTeamId { get; set; }
    public Guid? ToTeamId { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Payout { get; set; }
    public int? OldOverall { get; set; }
    public int? NewOverall { get; set; }
    public string? Notes { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    public Player Player { get; set; } = null!;
    public Team? FromTeam { get; set; }
    public Team? ToTeam { get; set; }
}
