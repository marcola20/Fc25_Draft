using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

public interface ITeamService
{
    Task<(IReadOnlyList<Team> items, int total)> SearchAsync(string? q, int page, int pageSize);
    Task<Team?> GetAsync(Guid id);
    Task<Guid> CreateAsync(TeamCreateDto dto);
    Task UpdateAsync(Guid id, TeamUpdateDto dto);
    Task DeleteAsync(Guid id);
    Task<string> RegenerateTokenAsync(Guid id);
    Task<string> RegenerateAuxTokenAsync(Guid id);
}
