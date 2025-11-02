using System;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IAuctionSettlementService
{
    Task<AuctionSettlementResult> SettleExpiredItemsAsync(Guid cycleId, CancellationToken ct);
    Task<AuctionSettlementResult> SettleAllOpenItemsOnCycleCloseAsync(Guid cycleId, bool forceClose, CancellationToken ct);
}
