using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ITransfersQueryService
{
    Task<PagedResult<TransferHistoryDto>> QueryHistoryAsync(TransfersFilter filter, CancellationToken ct);
}
