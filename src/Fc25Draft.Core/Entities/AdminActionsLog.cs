using System;

namespace Fc25Draft.Core.Entities;

public class AdminActionsLog
{
    public Guid ActionId { get; set; }
    public AdminActionType ActionType { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
