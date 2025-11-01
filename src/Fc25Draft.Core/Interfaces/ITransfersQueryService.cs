using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ITransfersQueryService
{
    Task<PagedResult<TransferListItemDto>> QueryHistoryAsync(TransfersFilter filter, CancellationToken ct);

    Task<TransferListItemDto?> GetByIdAsync(Guid transferId, CancellationToken ct);
}
