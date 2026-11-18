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

        // O admin vem pelo token de admin ou pelo token de treinador da pessoa ligada a ele.
        var adminToken = await _db.AdminPorTokenAsync(providedToken, Context.RequestAborted);

        var tokenDeAdmin = adminToken is not null && adminToken.Token == providedToken;
        if (tokenDeAdmin)
            MarcarUso(adminToken!.AdminTokenId);

        // O token é da pessoa: o clube sai da passagem que está em aberto
        // (o token de admin ligado a uma pessoa também leva ao clube dela).
        var acesso = await _db.AcessoPorTokenAsync(providedToken, Context.RequestAborted);

        // Token de admin sem clube por trás: entra só como admin.
        if (tokenDeAdmin && acesso is null)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, "Admin"),
                new(ClaimTypes.Role, "Admin"),
            };

            if (adminToken!.IsPrincipal)
                claims.Add(new Claim(ClaimTypes.Role, "AdminPrincipal"));

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }

        if (acesso is null)
        {
            // Sem clube a pessoa ainda entra — só não manda em time nenhum.
            var semClube = await _db.Treinadores
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Ativo && t.Token.ToUpper() == providedToken.ToUpper(), Context.RequestAborted);

            if (semClube is null)
                return AuthenticateResult.Fail("Token inválido.");

            var visitanteClaims = new List<Claim>
            {
                new(ClaimTypes.Name, semClube.Nome),
                new("TreinadorId", semClube.TreinadorId.ToString()),
            };
            AdicionarPapelDeAdmin(visitanteClaims, adminToken);

            var visitante = new ClaimsIdentity(visitanteClaims, Scheme.Name);

            return AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(visitante), Scheme.Name));
        }

        var team = await _db.Teams
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TeamId == acesso.TimeId, Context.RequestAborted);

        if (team is null)
            return AuthenticateResult.Fail("Token inválido.");

        var teamClaims = new List<Claim>
        {
            new(ClaimTypes.Name, acesso.Nome),
            new("TeamId", team.TeamId.ToString()),
            new("TeamName", team.TeamName),
            new("TreinadorId", acesso.TreinadorId.ToString()),
            new("Papel", acesso.Papel.ToString()),
        };

        if (team.IsAdmin && adminToken is null)
            teamClaims.Add(new Claim(ClaimTypes.Role, "Admin"));
        AdicionarPapelDeAdmin(teamClaims, adminToken);

        var teamIdentity = new ClaimsIdentity(teamClaims, Scheme.Name);
        var teamPrincipal = new ClaimsPrincipal(teamIdentity);
        var teamTicket = new AuthenticationTicket(teamPrincipal, Scheme.Name);

        return AuthenticateResult.Success(teamTicket);
    }

    /// <summary>Anota em segundo plano quando o token de admin foi usado (escopo próprio, sem disputar o DbContext).</summary>
    private void MarcarUso(Guid adminTokenId)
    {
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
                // Se falhar, não atrapalha o login.
            }
        });
    }

    /// <summary>Treinador que também é administrador: mesmo token, mais o papel de admin.</summary>
    private static void AdicionarPapelDeAdmin(List<Claim> claims, Fc25Draft.Core.Entities.AdminToken? admin)
    {
        if (admin is null) return;

        claims.Add(new Claim(ClaimTypes.Role, "Admin"));
        if (admin.IsPrincipal)
            claims.Add(new Claim(ClaimTypes.Role, "AdminPrincipal"));
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
