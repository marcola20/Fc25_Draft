using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Fc25Draft.Web.Services;

public class TeamAccessService
{
    private const string JsAccessor = "fc25Team";
    private readonly IJSRuntime _jsRuntime;
    private string? _token;
    private bool _initialized;

    public TeamAccessService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public event Action? TokenChanged;

    public async Task<string?> GetTokenAsync()
    {
        await EnsureInitializedAsync();
        return _token;
    }

    public async Task SetTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token inválido.", nameof(token));
        }

        await EnsureInitializedAsync();

        _token = token.Trim();

        try
        {
            await _jsRuntime.InvokeVoidAsync($"{JsAccessor}.setToken", _token);
        }
        catch (JSException)
        {
            // Ignored: storage may be unavailable.
        }

        TokenChanged?.Invoke();
    }

    public async Task ClearTokenAsync()
    {
        await EnsureInitializedAsync();
        _token = null;

        try
        {
            await _jsRuntime.InvokeVoidAsync($"{JsAccessor}.clearToken");
        }
        catch (JSException)
        {
            // Ignored
        }

        TokenChanged?.Invoke();
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            _token = await _jsRuntime.InvokeAsync<string?>($"{JsAccessor}.getToken");
            _initialized = true;
        }
        catch (JSException)
        {
            _token = null;
            _initialized = true;
        }
        catch (InvalidOperationException ex) when (IsPrerenderInteropException(ex))
        {
            // prerendering: skip initialization for now
        }
    }

    private static bool IsPrerenderInteropException(InvalidOperationException ex)
    {
        return ex.Message.Contains("JavaScript interop calls cannot be issued", StringComparison.Ordinal);
    }
}
