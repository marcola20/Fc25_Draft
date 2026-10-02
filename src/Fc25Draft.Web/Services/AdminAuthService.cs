using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Fc25Draft.Web.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Fc25Draft.Web.Services;

public class AdminAuthService
{
    private const string StorageKey = "fc25-admin-token";

    private readonly IJSRuntime _jsRuntime;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NavigationManager _navigationManager;
    private readonly LimiteDeTentativas _limite;
    private readonly OrigemDoCliente _origem;
    private string? _token;
    private bool _initialized;

    public AdminAuthService(
        IJSRuntime jsRuntime,
        IHttpClientFactory httpClientFactory,
        NavigationManager navigationManager,
        LimiteDeTentativas limite,
        OrigemDoCliente origem)
    {
        _jsRuntime = jsRuntime;
        _httpClientFactory = httpClientFactory;
        _navigationManager = navigationManager;
        _limite = limite;
        _origem = origem;
    }

    public event Action? AuthenticationChanged;

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_token);

    /// <summary>Já leu e validou o token guardado no navegador (no pré-render ainda é false).</summary>
    public bool IsInitialized => _initialized;

    public bool IsAdmin { get; private set; }

    /// <summary>O dono da liga: só ele gerencia os outros administradores.</summary>
    public bool IsPrincipal { get; private set; }

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
                    IsPrincipal = false;
                }
                else
                {
                    IsAdmin = result.IsAdmin;
                    IsPrincipal = result.IsPrincipal;
                }
            }

            initializationCompleted = true;
        }
        catch (JSException)
        {
            _token = null;
            IsAdmin = false;
            IsPrincipal = false;
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

    /// <summary>
    /// Login digitado pela pessoa: passa pelo limite de tentativas (3 tokens errados bloqueiam o IP por
    /// 15 minutos) e devolve a mensagem para mostrar, com quantas tentativas restam.
    /// </summary>
    public async Task<ResultadoDoLogin> EntrarAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new(false, "Informe o seu token.");

        if (_limite.BloqueadoPor(_origem.Ip) is TimeSpan falta)
            return new(false, LimiteDeTentativas.MensagemDeBloqueio(falta));

        var normalizedToken = token.Trim();
        var result = await ValidateTokenAsync(normalizedToken);
        if (!result.IsValid)
        {
            // Falha de rede ou do servidor não é chute: não gasta tentativa.
            return result.Recusado
                ? new(false, LimiteDeTentativas.MensagemDeErro(_limite.RegistrarErro(_origem.Ip)))
                : new(false, "Não foi possível validar o token. Tente novamente.");
        }

        await AplicarAsync(normalizedToken, result.IsAdmin, result.IsPrincipal);
        return new(true, null);
    }

    /// <summary>Entra com um token que o próprio sistema acabou de gerar (sem limite de tentativas).</summary>
    public async Task<bool> SignInAsync(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return false;

        var normalizedToken = token.Trim();
        var result = await ValidateTokenAsync(normalizedToken);
        if (!result.IsValid)
            return false;

        await AplicarAsync(normalizedToken, result.IsAdmin, result.IsPrincipal);
        return true;
    }

    private async Task AplicarAsync(string normalizedToken, bool isAdmin, bool isPrincipal)
    {
        _token = normalizedToken;
        IsAdmin = isAdmin;
        IsPrincipal = isPrincipal;

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
    }

    public async Task SignOutAsync()
    {
        _token = null;
        IsAdmin = false;
        IsPrincipal = false;

        await ClearStoredTokenAsync();

        _initialized = true;
        AuthenticationChanged?.Invoke();
    }

    /// <summary><c>Recusado</c>: a API disse que o token não vale (não foi falha de rede).</summary>
    private async Task<(bool IsValid, bool IsAdmin, bool IsPrincipal, bool Recusado)> ValidateTokenAsync(string token)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(_navigationManager.BaseUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            ChamadaInterna.Marcar(client);

            using var response = await client.GetAsync("api/auth/me");
            if (!response.IsSuccessStatusCode)
                return (false, false, false,
                    response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden);

            var data = await response.Content.ReadFromJsonAsync<AuthMeResponse>();
            return (true, data?.IsAdmin ?? false, data?.IsPrincipal ?? false, false);
        }
        catch (Exception)
        {
            return (false, false, false, false);
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

    private sealed record AuthMeResponse(bool IsAdmin, bool IsPrincipal);
}

public sealed record ResultadoDoLogin(bool Sucesso, string? Mensagem);
