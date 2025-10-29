using System;
using System.Collections.Generic;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

public interface ITransferHistoryService
{
    Task RegisterTransferAsync(TransferHistory entry);
    Task<IReadOnlyList<TransferHistory>> GetTransfersByTeamAsync(Guid teamId, int take = 50);
    Task<IReadOnlyList<TransferHistory>> GetRecentTransfersAsync(int take = 50);
}
