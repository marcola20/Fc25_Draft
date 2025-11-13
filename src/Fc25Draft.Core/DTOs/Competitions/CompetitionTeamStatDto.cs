namespace Fc25Draft.Core.DTOs.Competitions;

public sealed record CompetitionTeamStatDto(
    Guid CompetitionTeamStatId,
    Guid CompetitionTeamId,
    Guid TeamId,
    string TeamName,
    int MatchesPlayed,
    int Wins,
    int Draws,
    int Losses,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int YellowCards,
    int RedCards);
