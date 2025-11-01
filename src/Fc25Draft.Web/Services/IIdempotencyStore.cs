namespace Fc25Draft.Web.Services;

public interface IIdempotencyStore
{
    /// <summary>
    /// Attempts to register the specified key for the provided TTL.
    /// </summary>
    /// <param name="key">The idempotency key to register.</param>
    /// <param name="ttl">The time-to-live for the key.</param>
    /// <returns><c>true</c> when the key was registered for the first time; otherwise <c>false</c>.</returns>
    bool TryRegister(string key, TimeSpan ttl);
}
