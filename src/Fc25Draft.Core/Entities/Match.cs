namespace Fc25Draft.Core.Entities;

public class Match
{
    public Guid MatchId { get; set; }
    public Guid HomeTeamId { get; set; }
    public Guid AwayTeamId { get; set; }
    public DateTime KickoffAtUtc { get; set; }
    public string? HomeLineupSnapshotJson { get; set; }
    public string? AwayLineupSnapshotJson { get; set; }

    public Team HomeTeam { get; set; } = default!;
    public Team AwayTeam { get; set; } = default!;
}
