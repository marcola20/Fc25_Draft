using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;

namespace Fc25Draft.Infra.Services;

public class TransactionLogService : ITransactionLogService
{
    private readonly DraftDbContext _dbContext;

    public TransactionLogService(DraftDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task LogMarketAsync(
        MarketItem item,
        MarketTransactionType type,
        Guid? teamId,
        Guid? targetTeamId,
        decimal? amount,
        string performedBy,
        string? notes,
        DateTime occurredAtUtc,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (string.IsNullOrWhiteSpace(performedBy))
        {
            throw new ArgumentException("O identificador de quem realizou a ação é obrigatório.", nameof(performedBy));
        }

        var timestamp = occurredAtUtc.Kind switch
        {
            DateTimeKind.Utc => occurredAtUtc,
            DateTimeKind.Local => occurredAtUtc.ToUniversalTime(),
            _ => DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc)
        };

        var entry = new MarketTransaction
        {
            TransactionId = Guid.NewGuid(),
            CycleId = item.CycleId,
            ItemId = item.ItemId,
            PlayerId = item.PlayerId,
            TeamId = teamId,
            TargetTeamId = targetTeamId,
            Type = type,
            Amount = amount,
            PerformedBy = performedBy,
            Notes = notes,
            CreatedAtUtc = timestamp
        };

        await _dbContext.MarketTransactions.AddAsync(entry, ct).ConfigureAwait(false);
    }
}
