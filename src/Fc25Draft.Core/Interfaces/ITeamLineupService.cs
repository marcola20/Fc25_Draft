using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ITeamLineupService
{
    Task<IReadOnlyList<TeamLineupDto>> GetLineupsAsync(Guid teamId, CancellationToken ct);
    Task<TeamLineupDto> CreateLineupAsync(Guid teamId, TeamLineupSaveRequestDto request, CancellationToken ct);
    Task<TeamLineupDto> UpdateLineupAsync(Guid teamId, Guid lineupId, TeamLineupSaveRequestDto request, CancellationToken ct);
    Task DeleteLineupAsync(Guid teamId, Guid lineupId, CancellationToken ct);
    Task SetActiveLineupAsync(Guid teamId, Guid lineupId, CancellationToken ct);
    Task<IReadOnlyList<AdminLineupOverviewDto>> GetAdminLineupsAsync(Guid? teamId, CancellationToken ct);
}
