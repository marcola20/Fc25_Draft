using Fc25Draft.Core.DTOs.Seasons;

namespace Fc25Draft.Core.Interfaces;

public interface ISeasonQueryService
{
    Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync(CancellationToken ct);
    Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(Guid seasonId, CancellationToken ct);
    Task<IReadOnlyList<RoundDto>> GetRoundsAsync(Guid competitionId, CancellationToken ct);
    Task<IReadOnlyList<SeasonScheduleEntryDto>> GetScheduleAsync(Guid seasonId, CancellationToken ct);
}
