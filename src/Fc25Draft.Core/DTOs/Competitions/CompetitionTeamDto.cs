namespace Fc25Draft.Core.DTOs.Competitions;

public sealed record CompetitionTeamDto(
    Guid CompetitionTeamId,
    Guid TeamId,
    string TeamName,
    bool IsActive,
    decimal? InitialBudget,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
