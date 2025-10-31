using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketHistoryQueryService
{
    Task<PagedResult<MarketTransactionDto>> QueryAsync(MarketHistoryFilter filter, CancellationToken ct);
    Task<IReadOnlyList<MarketTransactionDto>> ExportAsync(MarketHistoryFilter filter, CancellationToken ct);
}
