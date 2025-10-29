using System;
using System.Threading.Tasks;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketSyncService
{
    Task ApplyWinningBidAsync(Guid itemId);
    Task ApplyTeamSaleAsync(int playerId, Guid fromTeamId, Guid toTeamId, decimal amount);
    Task ApplyTeamTradeAsync(int playerIdA, Guid teamA, int playerIdB, Guid teamB, decimal? balance = null);
}
