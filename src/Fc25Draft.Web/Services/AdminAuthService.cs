using Microsoft.JSInterop;

namespace Fc25Draft.Web.Services;

public class AdminAuthService
{
    private const string StorageKey = "fc25-admin-token";

    private readonly IJSRuntime _jsRuntime;
    private string? _token;
    private bool _initialized;

    public AdminAuthService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
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

        _token = token.Trim();

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

        try
        {
            await _jsRuntime.InvokeVoidAsync("fc25Auth.clearToken");
        }
        catch (JSException)
        {
            // Ignored on sign-out.
        }

        _initialized = true;
        AuthenticationChanged?.Invoke();
    }
}
