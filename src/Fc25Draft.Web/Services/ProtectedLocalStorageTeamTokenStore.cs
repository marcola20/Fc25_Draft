using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;

namespace Fc25Draft.Web.Services;

public class ProtectedLocalStorageTeamTokenStore : ITeamTokenStore
{
    private const string StorageKey = "fc25draft:team-token";

    private readonly ProtectedLocalStorage _storage;

    private string? _cachedToken;
    private bool _initialReadCompleted;

    public ProtectedLocalStorageTeamTokenStore(ProtectedLocalStorage storage)
    {
        _storage = storage;
    }

    public async Task<string?> GetAsync()
    {
        await EnsureInitialReadAsync();
        return _cachedToken;
    }

    public async Task SetAsync(string token)
    {
        _cachedToken = token;

        try
        {
            await _storage.SetAsync(StorageKey, token);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (JSException)
        {
            // Storage unavailable (e.g., disabled). Keep in-memory copy only.
        }
    }

    public async Task ClearAsync()
    {
        _cachedToken = null;

        try
        {
            await _storage.DeleteAsync(StorageKey);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (JSException)
        {
            // Ignore when storage cannot be accessed.
        }
    }

    public async Task<bool> IsConfiguredAsync()
    {
        await EnsureInitialReadAsync();
        return !string.IsNullOrWhiteSpace(_cachedToken);
    }

    private async Task EnsureInitialReadAsync()
    {
        if (_initialReadCompleted)
        {
            return;
        }

        try
        {
            var result = await _storage.GetAsync<string?>(StorageKey);
            if (result.Success)
            {
                _cachedToken = string.IsNullOrWhiteSpace(result.Value)
                    ? null
                    : result.Value;
            }

            _initialReadCompleted = true;
        }
        catch (InvalidOperationException)
        {
            // Protected browser storage cannot be accessed before first render.
            throw;
        }
        catch (JSException)
        {
            // Storage might be disabled. Treat as unavailable but do not retry.
            _initialReadCompleted = true;
        }
    }
}
