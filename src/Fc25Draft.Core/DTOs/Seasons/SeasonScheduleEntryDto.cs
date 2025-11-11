namespace Fc25Draft.Core.DTOs.Seasons;

public sealed record SeasonScheduleEntryDto(
    Guid SeasonScheduleItemId,
    Guid SeasonId,
    int Order,
    Guid CompetitionId,
    string CompetitionName,
    Guid RoundId,
    string RoundName,
    bool IsCompleted,
    DateTime? PlayedAtUtc,
    string? Notes);
