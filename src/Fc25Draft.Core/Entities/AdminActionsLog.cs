namespace Fc25Draft.Core.Entities;

public class AdminActionsLog
{
    public Guid ActionId { get; set; }
    public int ActionType { get; set; }
    public string PerformedBy { get; set; } = null!;
    public string PayloadJson { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
}
