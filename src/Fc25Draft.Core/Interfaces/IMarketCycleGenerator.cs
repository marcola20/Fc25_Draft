using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketCycleGenerator
{
    Task<MarketCycleDto> CreateNewCycleAsync(DateTime utcNow, CancellationToken ct);
    Task<bool> NeedsNewCycleAsync(DateTime utcNow, CancellationToken ct);
}
