using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.DTOs.Seasons;

namespace Fc25Draft.Core.Interfaces;

public interface IRoundSelectionService
{
    Task<RoundSelectionDto?> GetByRoundAsync(Guid roundId, CancellationToken ct);
    Task<RoundSelectionDto> CreateOrGetAsync(Guid roundId, CancellationToken ct);
    Task<OperationResultDto> AddPlayersAsync(Guid roundId, IEnumerable<Guid> playerIds, CancellationToken ct);
    Task<OperationResultDto> RemovePlayerAsync(Guid roundId, Guid playerId, CancellationToken ct);
}
