using System.Collections.Generic;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs.Competitions;

public sealed record CompetitionMatchDetailsDto(
    Guid CompetitionMatchId,
    Guid CompetitionId,
    Guid RoundId,
    Guid HomeCompetitionTeamId,
    string HomeTeamName,
    Guid AwayCompetitionTeamId,
    string AwayTeamName,
    DateTime? MatchDateUtc,
    int? HomeGoals,
    int? AwayGoals,
    CompetitionMatchStatus Status,
    string? Stadium,
    string? Observations,
    IReadOnlyList<CompetitionMatchEventDto> Events);

public sealed record CompetitionMatchEventDto(
    Guid CompetitionMatchEventId,
    Guid CompetitionTeamId,
    string TeamName,
    int? PlayerId,
    string? PlayerName,
    int? RelatedPlayerId,
    string? RelatedPlayerName,
    CompetitionMatchEventType EventType,
    int? Minute,
    string? Observations);
