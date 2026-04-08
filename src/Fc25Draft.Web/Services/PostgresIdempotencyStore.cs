using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Services;

// Persists idempotency keys in PostgreSQL so they survive application restarts.
// Uses IServiceScopeFactory (the standard singleton→scoped bridge) to resolve
// DraftDbContext, which is registered as scoped.
public sealed class PostgresIdempotencyStore : IIdempotencyStore
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public PostgresIdempotencyStore(IServiceScopeFactory scopeFactory, TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryRegister(string key, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Idempotency key cannot be null or whitespace.", nameof(key));
        if (ttl <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be greater than zero.");

        var now = _timeProvider.GetUtcNow();
        var expiration = now.Add(ttl);

        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<DraftDbContext>();

        // Single round-trip: delete expired entry for this key (if any), then INSERT.
        // ON CONFLICT DO NOTHING means a live key blocks the insert → returns 0 → false.
        var rows = ctx.Database.ExecuteSqlRaw(
            """
            WITH cleanup AS (
                DELETE FROM "IdempotencyKeys" WHERE "Key" = {0} AND "ExpiresAtUtc" <= {1}
            )
            INSERT INTO "IdempotencyKeys" ("Key", "ExpiresAtUtc")
            VALUES ({0}, {2})
            ON CONFLICT ("Key") DO NOTHING
            """,
            key, now, expiration);

        return rows > 0;
    }
}
