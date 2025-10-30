using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public class MarketTransaction
{
    public Guid TransactionId { get; set; }
    public Guid CycleId { get; set; }
    public Guid ItemId { get; set; }
    public int PlayerId { get; set; }
    public Guid? TeamId { get; set; }
    public Guid? TargetTeamId { get; set; }
    public MarketTransactionType Type { get; set; }
    public decimal? Amount { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public uint RowVersion { get; set; }
}
