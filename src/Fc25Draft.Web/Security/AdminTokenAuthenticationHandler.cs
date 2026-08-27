using System.Security.Claims;
using System.Text.Encodings.Web;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Web.Security;

public class AdminTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AdminToken";

    private readonly DraftDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;

    public AdminTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        DraftDbContext db,
        IServiceScopeFactory scopeFactory)
        : base(options, logger, encoder)
    {
        _db = db;
        _scopeFactory = scopeFactory;
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
            return AuthenticateResult.NoResult();

        const string bearerPrefix = "Bearer ";
        if (!headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        var providedToken = headerValue[bearerPrefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(providedToken))
            return AuthenticateResult.Fail("Token inválido.");

        // Check AdminTokens first (dedicated admin tokens without team requirement)
        var adminToken = await _db.AdminTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == providedToken && t.IsActive, Context.RequestAborted);

        if (adminToken is not null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "Admin"),
                new(ClaimTypes.Role, "Admin"),
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            // Update LastUsedAtUtc in background — uses its own scope to avoid concurrent DbContext access
            var adminTokenId = adminToken.AdminTokenId;
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
                    var tokenToUpdate = await db.AdminTokens.FirstOrDefaultAsync(t => t.AdminTokenId == adminTokenId);
                    if (tokenToUpdate is not null)
                    {
                        tokenToUpdate.LastUsedAtUtc = DateTime.UtcNow;
                        await db.SaveChangesAsync();
                    }
                }
                catch
                {
                    // Silent fail — don't break authentication if update fails
                }
            });

            return AuthenticateResult.Success(ticket);
        }

        // Fallback to Teams with IsAdmin flag (for backward compatibility)
        var team = await _db.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == providedToken || t.AuxToken == providedToken, Context.RequestAborted);

        if (team is null)
            return AuthenticateResult.Fail("Token inválido.");

        var teamClaims = new List<Claim>
        {
            new(ClaimTypes.Name, team.TeamName),
            new("TeamId", team.TeamId.ToString()),
        };

        if (team.IsAdmin)
            teamClaims.Add(new Claim(ClaimTypes.Role, "Admin"));

        var teamIdentity = new ClaimsIdentity(teamClaims, Scheme.Name);
        var teamPrincipal = new ClaimsPrincipal(teamIdentity);
        var teamTicket = new AuthenticationTicket(teamPrincipal, Scheme.Name);

        return AuthenticateResult.Success(teamTicket);
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
