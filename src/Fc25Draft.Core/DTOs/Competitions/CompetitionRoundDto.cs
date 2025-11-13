using System.Collections.Generic;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs.Competitions;

public sealed record CompetitionRoundDto(
    Guid RoundId,
    int RoundNumber,
    string Name,
    DateTime? ScheduledAtUtc,
    bool IsCompleted,
    DateTime? PlayedAtUtc,
    IReadOnlyList<CompetitionMatchDto> Matches);

public sealed record CompetitionMatchDto(
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
    string? Observations);
