using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IAdminTransferService
{
    Task<TransferResult> SellAsync(string adminToken, Guid fromTeamId, Guid toTeamId, Guid[] playerIds, decimal amount, string reason, CancellationToken ct);
    Task<TransferResult> SwapAsync(string adminToken, Guid teamAId, Guid[] playersFromA, Guid teamBId, Guid[] playersFromB, decimal cashAdjustFromAToB, string reason, CancellationToken ct);
    Task<TransferResult> MoveAsync(string adminToken, Guid playerId, Guid toTeamId, string reason, CancellationToken ct);
    Task<AdjustBudgetResult> AdjustBudgetAsync(string adminToken, Guid teamId, decimal delta, string reason, CancellationToken ct);
    Task<CancelItemResult> CancelMarketItemAsync(string adminToken, Guid itemId, string reason, CancellationToken ct);
}
