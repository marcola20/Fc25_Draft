namespace Fc25Draft.Core.Entities;

public sealed class CompetitionPlayerStat
{
    public Guid CompetitionPlayerStatId { get; set; }
    public Guid CompetitionId { get; set; }
    public Guid CompetitionTeamId { get; set; }
    public int PlayerId { get; set; }

    public int MatchesPlayed { get; set; }
    public int Goals { get; set; }
    public int Assists { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }

    public Competition Competition { get; set; } = default!;
    public CompetitionTeam CompetitionTeam { get; set; } = default!;
    public Player Player { get; set; } = default!;
}
