namespace Fc25Draft.Core.Entities;

public class TeamLineupChangeLog
{
    public Guid ChangeLogId { get; set; }
    public Guid LineupId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public string ChangesJson { get; set; } = null!;

    public TeamLineup Lineup { get; set; } = null!;
}
