using Fc25Draft.Core.Interfaces;
using Microsoft.Net.Http.Headers;

namespace Fc25Draft.Web.Extensions.Endpoints;

/// <summary>Fotos dos jogadores: a imagem pública e a importação do PES pelo script (admin).</summary>
public static class FotosEndpoints
{
    private static byte[]? _semFoto;

    public static IEndpointRouteBuilder MapFotosEndpoints(this IEndpointRouteBuilder app)
    {
        // Sem foto vai a silhueta, com o mesmo cache: a tela não precisa saber quem tem foto.
        app.MapGet("/fotos/jogadores/{playerId:int}", async (
            int playerId, HttpContext ctx, IFotosJogadoresService fotos, IWebHostEnvironment env, CancellationToken ct) =>
        {
            ctx.Response.Headers.CacheControl = "public, max-age=600";
            var foto = await fotos.ObterAsync(playerId, ct);
            if (foto is null)
            {
                _semFoto ??= await File.ReadAllBytesAsync(Path.Combine(env.WebRootPath, "images", "jogador-sem-foto.svg"), ct);
                return Results.File(_semFoto, "image/svg+xml");
            }

            return Results.File(foto.Imagem, foto.ContentType,
                lastModified: DateTime.SpecifyKind(foto.AtualizadaEm, DateTimeKind.Utc),
                entityTag: new EntityTagHeaderValue($"\"{foto.AtualizadaEm.Ticks:x}\""));
        }).AllowAnonymous();

        // Usado pelo scripts/pes/enviar_fotos_pes.py, com o token de admin.
        var admin = app.MapGroup("/api/admin/fotos-pes").RequireAuthorization("AdminOnly");

        admin.MapGet("/pendentes", async (IFotosJogadoresService fotos, CancellationToken ct) =>
            Results.Ok(await fotos.PesIdsParaImportarAsync(ct)));

        admin.MapPost("/", async (List<FotoPesRequest> envio, IFotosJogadoresService fotos, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(new { gravadas = await fotos.ImportarDoPesAsync(envio, ct) });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return app;
    }
}
