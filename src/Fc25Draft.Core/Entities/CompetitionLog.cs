namespace Fc25Draft.Core.Entities;

public sealed class CompetitionLog
{
    public Guid CompetitionLogId { get; set; }
    public Guid? CompetitionId { get; set; }
    public Guid? CompetitionMatchId { get; set; }
    public string Action { get; set; } = default!;
    public string? PerformedBy { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Competition? Competition { get; set; }
    public CompetitionMatch? Match { get; set; }
}
