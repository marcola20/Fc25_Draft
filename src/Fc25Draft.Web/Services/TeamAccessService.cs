using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Web.Services;

public class TeamAccessService
{
    private const int MaxTokenLength = 80;

    private readonly ITeamTokenStore _store;
    private readonly ILogger<TeamAccessService> _logger;

    private string? _token;
    private bool _initialized;
    private string? _invalidTokenMessage;

    public TeamAccessService(ITeamTokenStore store, ILogger<TeamAccessService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public event Action? TokenChanged;

    public async Task<string?> GetTokenAsync()
    {
        await EnsureInitializedAsync();
        return _token;
    }

    public async Task<bool> IsConfiguredAsync()
    {
        await EnsureInitializedAsync();
        return !string.IsNullOrWhiteSpace(_token);
    }

    public async Task SetTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token inválido.", nameof(token));
        }

        var normalized = token.Trim();
        if (normalized.Length > MaxTokenLength)
        {
            throw new ArgumentException($"Token deve ter no máximo {MaxTokenLength} caracteres.", nameof(token));
        }

        await EnsureInitializedAsync();

        _token = normalized;

        try
        {
            await _store.SetAsync(normalized);
        }
        catch (InvalidOperationException ex) when (IsPrerenderInteropException(ex))
        {
            _logger.LogDebug(ex, "Armazenamento protegido indisponível antes do primeiro render.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir token do time no armazenamento protegido.");
        }

        TokenChanged?.Invoke();
    }

    public async Task ClearTokenAsync()
    {
        await EnsureInitializedAsync();
        _token = null;

        try
        {
            await _store.ClearAsync();
        }
        catch (InvalidOperationException ex) when (IsPrerenderInteropException(ex))
        {
            _logger.LogDebug(ex, "Armazenamento protegido indisponível antes do primeiro render.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao limpar token do time no armazenamento protegido.");
        }

        TokenChanged?.Invoke();
    }

    public void ReportInvalidToken(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _invalidTokenMessage = message.Trim();
    }

    public string? ConsumeInvalidTokenMessage()
    {
        var message = _invalidTokenMessage;
        _invalidTokenMessage = null;
        return message;
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            _token = await _store.GetAsync();
            if (!string.IsNullOrWhiteSpace(_token))
            {
                _token = _token.Trim();
            }
            _initialized = true;
        }
        catch (InvalidOperationException ex) when (IsPrerenderInteropException(ex))
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar token do time armazenado.");
            _token = null;
            _initialized = true;
        }
    }

    private static bool IsPrerenderInteropException(InvalidOperationException ex)
    {
        return ex.Message.Contains("JavaScript interop calls cannot be issued", StringComparison.Ordinal)
            || ex.Message.Contains("JavaScript interop calls cannot be issued because the component is prerendering", StringComparison.Ordinal);
    }
}
