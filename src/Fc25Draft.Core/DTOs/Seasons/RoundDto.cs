namespace Fc25Draft.Core.DTOs.Seasons;

public sealed record RoundDto(
    Guid RoundId,
    Guid CompetitionId,
    string Name,
    bool IsCompleted,
    DateTime? PlayedAtUtc,
    string? Notes);
