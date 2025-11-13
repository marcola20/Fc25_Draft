namespace Fc25Draft.Core.DTOs.Competitions;

public sealed record CompetitionStandingDto(
    Guid CompetitionStandingId,
    Guid CompetitionTeamId,
    Guid TeamId,
    string TeamName,
    int Position,
    int MatchesPlayed,
    int Wins,
    int Draws,
    int Losses,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points,
    int YellowCards,
    int RedCards,
    DateTime UpdatedAtUtc);
