using System.Security.Claims;
using System.Text.Encodings.Web;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Web.Security;

public class AdminTokenAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AdminToken";

    private readonly DraftDbContext _db;

    public AdminTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DraftDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task InitializeHandlerAsync()
    {
        await base.InitializeHandlerAsync();
        Options.TimeProvider ??= TimeProvider.System;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            return AuthenticateResult.NoResult();

        var headerValue = authorizationHeader.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue))
            return AuthenticateResult.Fail("Cabeçalho de autorização ausente.");

        const string bearerPrefix = "Bearer ";
        if (!headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.Fail("Formato de autorização inválido.");

        var providedToken = headerValue[bearerPrefix.Length..].Trim();
        if (!Guid.TryParse(providedToken, out var tokenGuid))
            return AuthenticateResult.Fail("Token de administrador inválido.");

        var tokenExists = await _db.AdminTokens
            .AsNoTracking()
            .AnyAsync(t => t.Token == tokenGuid);

        if (!tokenExists)
            return AuthenticateResult.Fail("Token de administrador inválido.");

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "Administrador"),
            new Claim(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers["WWW-Authenticate"] = Scheme.Name;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
