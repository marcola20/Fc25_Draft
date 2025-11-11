namespace Fc25Draft.Core.DTOs.Seasons;

public sealed record CompetitionDto(
    Guid CompetitionId,
    Guid SeasonId,
    string Name,
    int Order,
    bool IsActive);
