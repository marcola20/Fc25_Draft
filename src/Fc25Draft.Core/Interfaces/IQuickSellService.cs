using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IQuickSellService
{
    Task<QuickSellResultDto> QuickSellAsync(Guid teamId, int playerId, string teamToken, CancellationToken ct = default);
}
