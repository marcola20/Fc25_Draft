using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ITeamLineupService
{
    Task<TeamLineupResponse?> GetActiveAsync(Guid teamId, CancellationToken ct);
    Task<TeamLineupResponse?> GetByIdAsync(Guid teamId, Guid lineupId, CancellationToken ct);
    Task<IReadOnlyList<TeamLineupSummaryResponse>> GetSummariesAsync(Guid teamId, CancellationToken ct);
    Task<TeamLineupResponse> SaveAsync(Guid teamId, SaveLineupRequest request, CancellationToken ct);
    Task SetActiveAsync(Guid teamId, Guid lineupId, CancellationToken ct);
    Task DeleteAsync(Guid teamId, Guid lineupId, CancellationToken ct);
    Task<IReadOnlyList<LineupSlotTemplateDto>> BuildTemplateAsync(string formationCode, CancellationToken ct);
}
