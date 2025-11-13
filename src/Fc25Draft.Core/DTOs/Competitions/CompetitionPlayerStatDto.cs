namespace Fc25Draft.Core.DTOs.Competitions;

public sealed record CompetitionPlayerStatDto(
    Guid CompetitionPlayerStatId,
    Guid CompetitionTeamId,
    Guid TeamId,
    string TeamName,
    int PlayerId,
    string PlayerName,
    int MatchesPlayed,
    int Goals,
    int Assists,
    int YellowCards,
    int RedCards);
