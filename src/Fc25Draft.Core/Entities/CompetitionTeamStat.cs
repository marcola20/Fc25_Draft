namespace Fc25Draft.Core.Entities;

public sealed class CompetitionTeamStat
{
    public Guid CompetitionTeamStatId { get; set; }
    public Guid CompetitionId { get; set; }
    public Guid CompetitionTeamId { get; set; }

    public int MatchesPlayed { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int GoalDifference { get; set; }
    public int Wins { get; set; }
    public int Draws { get; set; }
    public int Losses { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }

    public Competition Competition { get; set; } = default!;
    public CompetitionTeam CompetitionTeam { get; set; } = default!;
}
