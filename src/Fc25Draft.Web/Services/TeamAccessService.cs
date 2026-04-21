using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Web.Services;

public class TeamAccessService
{
    private readonly AdminAuthService _authService;
    private readonly ILogger<TeamAccessService> _logger;
    private string? _invalidTokenMessage;

    public TeamAccessService(AdminAuthService authService, ILogger<TeamAccessService> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    public event Action? TokenChanged
    {
        add => _authService.AuthenticationChanged += value;
        remove => _authService.AuthenticationChanged -= value;
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _authService.GetTokenAsync();
    }

    public async Task<bool> IsConfiguredAsync()
    {
        await _authService.EnsureInitializedAsync();
        return _authService.IsAuthenticated;
    }

    public async Task SetTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token inválido.", nameof(token));

        var success = await _authService.SignInAsync(token);
        if (!success)
            throw new InvalidOperationException("Token inválido ou não encontrado.");
    }

    public async Task ClearTokenAsync()
    {
        await _authService.SignOutAsync();
    }

    public void ReportInvalidToken(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            _invalidTokenMessage = message.Trim();
    }

    public string? ConsumeInvalidTokenMessage()
    {
        var message = _invalidTokenMessage;
        _invalidTokenMessage = null;
        return message;
    }
}
