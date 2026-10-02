using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Security;

/// <summary>
/// Na API, quem chama de fora com token (Bearer ou X-Team-Token) passa pelo limite de tentativas:
/// token que não existe conta um erro, e o IP bloqueado recebe 429 antes de chegar no endpoint.
/// Roda depois da autenticação, que já validou o Bearer.
/// </summary>
public sealed class LimiteDeTentativasMiddleware
{
    private readonly RequestDelegate _next;

    public LimiteDeTentativasMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx, LimiteDeTentativas limite, DraftDbContext db)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api") || ChamadaInterna.EhInterna(ctx.Request))
        {
            await _next(ctx);
            return;
        }

        var bearer = TokenBearer(ctx.Request);
        var tokenDoTime = ctx.Request.Headers["X-Team-Token"].FirstOrDefault()?.Trim();
        if (bearer is null && string.IsNullOrEmpty(tokenDoTime))
        {
            await _next(ctx);
            return;
        }

        var origem = ctx.Connection.RemoteIpAddress?.ToString() ?? "desconhecida";
        if (limite.BloqueadoPor(origem) is TimeSpan falta)
        {
            await ResponderAsync(ctx, StatusCodes.Status429TooManyRequests, LimiteDeTentativas.MensagemDeBloqueio(falta));
            return;
        }

        var valido = (bearer is null || ctx.User.Identity?.IsAuthenticated == true)
            && (string.IsNullOrEmpty(tokenDoTime) || await TokenExisteAsync(db, tokenDoTime, ctx.RequestAborted));
        if (!valido)
        {
            var restam = limite.RegistrarErro(origem);
            await ResponderAsync(ctx,
                restam > 0 ? StatusCodes.Status401Unauthorized : StatusCodes.Status429TooManyRequests,
                LimiteDeTentativas.MensagemDeErro(restam));
            return;
        }

        await _next(ctx);
    }

    private static string? TokenBearer(HttpRequest request)
    {
        var valor = request.Headers.Authorization.FirstOrDefault();
        const string prefixo = "Bearer ";
        return valor is not null && valor.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase)
            ? valor[prefixo.Length..].Trim()
            : null;
    }

    // Token de treinador ativo (com ou sem clube) ou de admin: existir já basta, não é chute.
    private static async Task<bool> TokenExisteAsync(DraftDbContext db, string token, CancellationToken ct)
    {
        var maiusculo = token.ToUpperInvariant();
        return await db.Treinadores.AsNoTracking().AnyAsync(t => t.Ativo && t.Token.ToUpper() == maiusculo, ct)
            || await db.AdminTokens.AsNoTracking().AnyAsync(t => t.IsActive && t.Token == token, ct);
    }

    private static Task ResponderAsync(HttpContext ctx, int status, string mensagem)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { message = mensagem }, ctx.RequestAborted);
    }
}
