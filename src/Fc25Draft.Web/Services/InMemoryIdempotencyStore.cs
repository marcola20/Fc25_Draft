using System.Collections.Concurrent;

namespace Fc25Draft.Web.Services;

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    public InMemoryIdempotencyStore(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryRegister(string key, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Idempotency key cannot be null or whitespace.", nameof(key));
        }

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "TTL must be greater than zero.");
        }

        var now = _timeProvider.GetUtcNow();
        CleanupExpired(now);

        var expiration = now.Add(ttl);

        while (true)
        {
            if (_entries.TryGetValue(key, out var existingExpiration))
            {
                if (existingExpiration > now)
                {
                    return false;
                }

                _entries.TryRemove(key, out _);
                continue;
            }

            if (_entries.TryAdd(key, expiration))
            {
                return true;
            }
        }
    }

    private void CleanupExpired(DateTimeOffset now)
    {
        foreach (var entry in _entries)
        {
            if (entry.Value <= now)
            {
                _entries.TryRemove(entry.Key, out _);
            }
        }
    }
}
