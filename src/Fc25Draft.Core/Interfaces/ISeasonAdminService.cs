using Fc25Draft.Core.DTOs.Seasons;

namespace Fc25Draft.Core.Interfaces;

public interface ISeasonAdminService
{
    Task<SeasonDto> CreateSeasonAsync(SeasonUpsertCommand command, CancellationToken ct);
    Task<SeasonDto?> UpdateSeasonAsync(Guid seasonId, SeasonUpsertCommand command, CancellationToken ct);
    Task<bool> DeleteSeasonAsync(Guid seasonId, CancellationToken ct);

    Task<CompetitionDto> CreateCompetitionAsync(Guid seasonId, CompetitionUpsertCommand command, CancellationToken ct);
    Task<CompetitionDto?> UpdateCompetitionAsync(Guid competitionId, CompetitionUpsertCommand command, CancellationToken ct);
    Task<bool> DeleteCompetitionAsync(Guid competitionId, CancellationToken ct);

    Task<RoundDto> CreateRoundAsync(Guid competitionId, RoundUpsertCommand command, CancellationToken ct);
    Task<RoundDto?> UpdateRoundAsync(Guid roundId, RoundUpsertCommand command, CancellationToken ct);
    Task<RoundDto?> UpdateRoundCompletionAsync(Guid roundId, RoundCompletionCommand command, CancellationToken ct);
    Task<bool> DeleteRoundAsync(Guid roundId, CancellationToken ct);

    Task<IReadOnlyList<SeasonScheduleEntryDto>> UpdateSeasonScheduleAsync(SeasonScheduleUpdateCommand command, CancellationToken ct);
}
