using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;

namespace Fc25Draft.Web.Extensions.Endpoints;

/// <summary>
/// A fila de artes para o Artista CBFV (agente no PC do admin), com o token de admin no Authorization:
///   GET  /api/admin/artes/pendentes        procura fatos novos e devolve os pedidos com os dados prontos
///   POST /api/admin/artes/{id}/concluir    { "ok": true } ou { "ok": false, "erro": "..." }
/// </summary>
public static class ArtesEndpoints
{
    public static IEndpointRouteBuilder MapArtesEndpoints(this IEndpointRouteBuilder app)
    {
        var artes = app.MapGroup("/admin/artes").RequireAuthorization("AdminOnly");

        artes.MapGet("/pendentes", async (IArtesService svc, CancellationToken ct) =>
            Results.Ok(await svc.PendentesAsync(ct)));

        artes.MapPost("/{id:guid}/concluir", async (Guid id, ConcluirPedidoArteRequest corpo, IArtesService svc, CancellationToken ct) =>
        {
            try
            {
                await svc.ConcluirAsync(id, corpo.Ok, corpo.Erro, ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { message = ex.Message });
            }
        });

        return app;
    }
}
