using Microsoft.AspNetCore.Mvc;
using Fc25Draft.Core.Interfaces;

namespace Fc25Draft.Web.Extensions.Endpoints
{
    public static class PricingEndpoints
    {
        public static IEndpointRouteBuilder MapPricingEndpoints(this IEndpointRouteBuilder api)
        {
            var pricingApi = api.MapGroup("/pricing");

            pricingApi.MapGet(
                "/preview",
                async (
                    [FromQuery(Name = "pos")] string? positionCode,
                    [FromQuery(Name = "posId")] short? positionId,
                    [FromQuery] int? age,
                    [FromQuery(Name = "ovr")] int? overall,
                    IPricingService pricingService,
                    CancellationToken ct) =>
                {
                    if (!age.HasValue)
                        return Results.BadRequest(new { message = "Parâmetro 'age' é obrigatório." });

                    if (!overall.HasValue)
                        return Results.BadRequest(new { message = "Parâmetro 'ovr' é obrigatório." });

                    if (string.IsNullOrWhiteSpace(positionCode) && !positionId.HasValue)
                        return Results.BadRequest(new { message = "Informe 'pos' ou 'posId'." });

                    try
                    {
                        var result = await pricingService.CalculateForPositionAsync(
                            positionCode, positionId, age.Value, overall.Value, ct);

                        return Results.Ok(result);
                    }
                    catch (ArgumentException ex)
                    {
                        return Results.BadRequest(new { message = ex.Message });
                    }
                });

            pricingApi.MapGet(
                "/preview/{playerId:int}",
                async (int playerId, IPricingService pricingService, CancellationToken ct) =>
                {
                    try
                    {
                        var result = await pricingService.CalculateForPlayerAsync(playerId, ct);
                        return Results.Ok(result);
                    }
                    catch (KeyNotFoundException)
                    {
                        return Results.NotFound(new { message = $"Jogador {playerId} não encontrado." });
                    }
                    catch (InvalidOperationException ex)
                    {
                        return Results.BadRequest(new { message = ex.Message });
                    }
                    catch (ArgumentException ex)
                    {
                        return Results.BadRequest(new { message = ex.Message });
                    }
                });

            return api;
        }
    }
}
