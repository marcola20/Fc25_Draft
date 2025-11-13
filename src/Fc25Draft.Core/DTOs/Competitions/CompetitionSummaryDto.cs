using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.DTOs.Competitions;

public sealed record CompetitionSummaryDto(
    Guid CompetitionId,
    Guid SeasonId,
    string SeasonName,
    string Name,
    int Order,
    CompetitionType Type,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int TotalTeams,
    int TotalRounds,
    int TotalMatches);
