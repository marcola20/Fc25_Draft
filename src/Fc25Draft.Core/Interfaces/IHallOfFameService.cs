using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IHallOfFameService
{
    Task<IReadOnlyList<HallOfFameEntryDto>> GetAllAsync(CancellationToken ct);
    Task<HallOfFameEntryDto> CreateAsync(HallOfFameCreateRequest request, CancellationToken ct);
    Task<HallOfFameEntryDto> UpdateAsync(Guid id, HallOfFameUpdateRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}
