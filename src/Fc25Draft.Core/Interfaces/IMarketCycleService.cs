using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketCycleService
{
    Task<MarketCycleDto?> ResolveAsync(Guid? cycleId, CancellationToken ct);

    Task<IReadOnlyList<MarketCycleDto>> ListActiveAsync(CancellationToken ct);
}
