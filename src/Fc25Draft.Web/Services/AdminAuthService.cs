using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fc25Draft.Web.Services;

public class AdminAuthService
{
    private const string StorageKey = "fc25-admin-token";

    private readonly IJSRuntime _jsRuntime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NavigationManager _navigationManager;
    private string? _token;
    private bool _initialized;

    public AdminAuthService(
        IJSRuntime jsRuntime,
        IHttpClientFactory httpClientFactory,
        NavigationManager navigationManager)
    {
        _jsRuntime = jsRuntime;
        _httpClientFactory = httpClientFactory;
        _navigationManager = navigationManager;
    }

    public event Action? AuthenticationChanged;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_token);

    public bool IsAdmin { get; private set; }

    public string? Token => _token;

    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        var initializationCompleted = false;

        try
        {
            _token = await _jsRuntime.InvokeAsync<string?>("fc25Auth.getToken");
            if (!string.IsNullOrWhiteSpace(_token))
            {
                var result = await ValidateTokenAsync(_token);
                if (!result.IsValid)
                {
                    await ClearStoredTokenAsync();
                    _token = null;
                    IsAdmin = false;
                }
                else
                {
                    IsAdmin = result.IsAdmin;
                }
            }

            initializationCompleted = true;
        }
        catch (JSException)
        {
            _token = null;
            IsAdmin = false;
            initializationCompleted = true;
        }
        catch (InvalidOperationException ex) when (IsPrerenderInteropException(ex))
        {
            return;
        }

        if (initializationCompleted)
        {
            _initialized = true;
            AuthenticationChanged?.Invoke();
        }
    }

    private static bool IsPrerenderInteropException(InvalidOperationException ex)
    {
        return ex.Message.Contains(
            "JavaScript interop calls cannot be issued",
            StringComparison.Ordinal);
    }

    public async Task<string?> GetTokenAsync()
    {
        await EnsureInitializedAsync();
        return _token;
    }

    public async Task<bool> SignInAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var normalizedToken = token.Trim();
        var result = await ValidateTokenAsync(normalizedToken);
        if (!result.IsValid)
            return false;

        _token = normalizedToken;
        IsAdmin = result.IsAdmin;

        try
        {
            await _jsRuntime.InvokeVoidAsync("fc25Auth.setToken", _token);
        }
        catch (JSException)
        {
            // Keep token in memory only if storage fails.
        }

        _initialized = true;
        AuthenticationChanged?.Invoke();
        return true;
    }

    public async Task SignOutAsync()
    {
        _token = null;
        IsAdmin = false;

        await ClearStoredTokenAsync();

        _initialized = true;
        AuthenticationChanged?.Invoke();
    }

    private async Task<(bool IsValid, bool IsAdmin)> ValidateTokenAsync(string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_navigationManager.BaseUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.GetAsync("api/auth/me");
            if (!response.IsSuccessStatusCode)
                return (false, false);

            var data = await response.Content.ReadFromJsonAsync<AuthMeResponse>();
            return (true, data?.IsAdmin ?? false);
        }
        catch (Exception)
        {
            return (false, false);
        }
    }

    private async Task ClearStoredTokenAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("fc25Auth.clearToken");
        }
        catch (Exception)
        {
            // Storage may be unavailable.
        }
    }

    private sealed record AuthMeResponse(bool IsAdmin);
}
