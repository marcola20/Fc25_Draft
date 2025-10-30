using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Interfaces;

public interface ITransactionLogService
{
    Task LogMarketAsync(
        MarketItem item,
        MarketTransactionType type,
        Guid? teamId,
        Guid? targetTeamId,
        decimal? amount,
        string performedBy,
        string? notes,
        DateTime occurredAtUtc,
        CancellationToken ct);
}
