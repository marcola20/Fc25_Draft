using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketCycleAdminService
{
    Task<MarketCycleDto> CreateAsync(MarketCycleCreateCommand command, CancellationToken ct);

    Task<PagedResult<MarketCycleDto>> QueryAsync(MarketCycleQuery command, CancellationToken ct);

    Task<MarketCycleDto?> GetByIdAsync(Guid cycleId, CancellationToken ct);

    Task<MarketCycleDto> UpdateStatusAsync(Guid cycleId, MarketCycleStatus status, bool forceClose, CancellationToken ct);

    Task<MarketCycleStatusUpdateResult> ConcludeAsync(Guid cycleId, CancellationToken ct);
}

public record MarketCycleCreateCommand(
    string Name,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    MarketCycleStatus Status,
    string? Notes);

public record MarketCycleQuery(
    int Page,
    int PageSize,
    MarketCycleStatus? Status,
    DateTime? StartsAfterUtc,
    DateTime? StartsBeforeUtc);
