using System;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ITeamQuickSellService
{
    Task<QuickSellResultDto> QuickSellAsync(Guid teamId, Guid playerId, string teamToken, CancellationToken ct);
}
