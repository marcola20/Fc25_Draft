using System;
using System.Net.Http;
using System.Net.Http.Headers;
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

    public string? Token => _token;

    public async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            _token = await _jsRuntime.InvokeAsync<string?>("fc25Auth.getToken");
            if (!string.IsNullOrWhiteSpace(_token))
            {
                var valid = await ValidateTokenAsync(_token);
                if (!valid)
                {
                    await ClearStoredTokenAsync();
                    _token = null;
                }
            }
        }
        catch (JSException)
        {
            _token = null;
        }

        _initialized = true;
        AuthenticationChanged?.Invoke();
    }

    public async Task<string?> GetTokenAsync()
    {
        await EnsureInitializedAsync();
        return _token;
    }

    public async Task<bool> SignInAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var normalizedToken = token.Trim();
        var isValid = await ValidateTokenAsync(normalizedToken);
        if (!isValid)
        {
            return false;
        }

        _token = normalizedToken;

        try
        {
            await _jsRuntime.InvokeVoidAsync("fc25Auth.setToken", _token);
        }
        catch (JSException)
        {
            // Ignored: fallback is to keep token only in memory for current sessão.
        }

        _initialized = true;
        AuthenticationChanged?.Invoke();
        return true;
    }

    public async Task SignOutAsync()
    {
        _token = null;

        await ClearStoredTokenAsync();

        _initialized = true;
        AuthenticationChanged?.Invoke();
    }

    private async Task<bool> ValidateTokenAsync(string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_navigationManager.BaseUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.GetAsync("api/admin/validate");
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
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
            // Ignored: storage may be unavailable.
        }
    }
}
