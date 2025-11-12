using Fc25Draft.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Web.Extensions.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder api)
    {
        var matchesApi = api.MapGroup("/matches");

        matchesApi.MapPost("/{matchId:guid}/capture-lineup", async (
        Guid matchId,
        IMatchService matchService,
        ILoggerFactory loggerFactory,
        CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("MatchEndpoints");

                try
                {
                    await matchService.CaptureLineupsAsync(matchId, ct);
                    return Results.NoContent();
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogWarning(ex, "Partida {MatchId} não encontrada ao capturar escalações.", matchId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status404NotFound);
                }
                catch (ArgumentException ex)
                {
                    logger.LogWarning(ex, "Parâmetros inválidos ao capturar escalações da partida {MatchId}.", matchId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status400BadRequest);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Erro inesperado ao capturar escalações da partida {MatchId}.", matchId);
                    return Results.Json(new { message = "Erro interno no servidor." }, statusCode: StatusCodes.Status500InternalServerError);
                }
            });

        return api;
    }
}
