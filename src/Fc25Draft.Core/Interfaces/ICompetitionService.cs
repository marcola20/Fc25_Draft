using Fc25Draft.Core.DTOs.Competitions;

namespace Fc25Draft.Core.Interfaces;

public interface ICompetitionService
{
    Task<IReadOnlyList<CompetitionSummaryDto>> GetCompetitionsAsync(CancellationToken ct);
    Task<CompetitionDetailsDto> GetCompetitionDetailsAsync(Guid competitionId, CancellationToken ct);
    Task<CompetitionSummaryDto> CreateCompetitionAsync(CompetitionCreateCommand command, string? performedBy, CancellationToken ct);
    Task<CompetitionSummaryDto?> UpdateCompetitionAsync(Guid competitionId, CompetitionUpdateCommand command, string? performedBy, CancellationToken ct);
    Task<bool> SetCompetitionActiveAsync(Guid competitionId, bool isActive, string? performedBy, CancellationToken ct);
    Task<IReadOnlyList<CompetitionTeamDto>> GetTeamsAsync(Guid competitionId, CancellationToken ct);
    Task<CompetitionTeamDto> AddTeamAsync(Guid competitionId, CompetitionTeamAssignCommand command, string? performedBy, CancellationToken ct);
    Task<bool> RemoveTeamAsync(Guid competitionTeamId, string? performedBy, CancellationToken ct);
    Task<IReadOnlyList<CompetitionRoundDto>> GenerateRoundsAsync(Guid competitionId, CompetitionRoundGenerationCommand command, string? performedBy, CancellationToken ct);
    Task<IReadOnlyList<CompetitionRoundDto>> GetRoundsAsync(Guid competitionId, CancellationToken ct);
    Task<CompetitionMatchDetailsDto> UpsertMatchAsync(CompetitionMatchUpsertCommand command, string? performedBy, CancellationToken ct);
    Task<bool> DeleteMatchAsync(Guid competitionMatchId, string? performedBy, CancellationToken ct);
    Task<CompetitionMatchDetailsDto?> GetMatchDetailsAsync(Guid competitionMatchId, CancellationToken ct);
    Task<CompetitionMatchDetailsDto> ReplaceMatchEventsAsync(Guid competitionMatchId, IReadOnlyCollection<CompetitionMatchEventUpsertCommand> events, string? performedBy, CancellationToken ct);
    Task<IReadOnlyList<CompetitionStandingDto>> GetStandingsAsync(Guid competitionId, CancellationToken ct);
    Task<IReadOnlyList<CompetitionStandingDto>> RebuildStandingsAsync(Guid competitionId, string? performedBy, CancellationToken ct);
    Task<IReadOnlyList<CompetitionPlayerStatDto>> GetPlayerStatsAsync(Guid competitionId, CancellationToken ct);
    Task<IReadOnlyList<CompetitionTeamStatDto>> GetTeamStatsAsync(Guid competitionId, CancellationToken ct);
}
