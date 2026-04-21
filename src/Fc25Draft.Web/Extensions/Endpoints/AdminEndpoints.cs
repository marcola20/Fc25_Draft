// Extensions/Endpoints/AdminEndpoints.cs
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Services;
using Fc25Draft.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Linq;
using System.Threading;
using static Fc25Draft.Web.Extensions.EndpointHelpers; // TryGetAdminToken, etc.

namespace Fc25Draft.Web.Extensions.Endpoints
{
    public static class AdminEndpoints
    {
        public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/auth/me", (HttpContext ctx) =>
            {
                var teamIdClaim = ctx.User.FindFirst("TeamId")?.Value;
                var teamName = ctx.User.Identity?.Name;
                var isAdmin = ctx.User.IsInRole("Admin");
                return Results.Ok(new { status = "ok", teamName, isAdmin, teamId = teamIdClaim });
            }).RequireAuthorization();

            var adminApi = app.MapGroup("/admin").RequireAuthorization("AdminOnly");

            adminApi.MapGet("/validate", () => Results.Ok(new { status = "ok" }));

            adminApi.MapGet("/lineups", async (
                Guid? teamId,
                ITeamLineupService lineupService,
                ILoggerFactory loggerFactory,
                CancellationToken ct) =>
            {
                var logger = loggerFactory.CreateLogger("AdminLineupsEndpoints");

                try
                {
                    var lineups = await lineupService.GetAdminLineupsAsync(teamId, ct);
                    return Results.Ok(lineups);
                }
                catch (KeyNotFoundException ex)
                {
                    logger.LogWarning(ex, "AdminLineups:Team not found {TeamId}", teamId);
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status404NotFound);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "AdminLineups:Unexpected error for team {TeamId}", teamId);
                    return Results.Json(new { message = "Não foi possível carregar as escalações." }, statusCode: StatusCodes.Status500InternalServerError);
                }
            });

            var adminTransferApi = adminApi.MapGroup("/transfer");

            adminTransferApi.MapPost("/sell", async (
                HttpContext httpContext,
                AdminSellPlayersRequestDto request,
                AdminTransferService adminTransferService,
                CancellationToken ct) =>
            {
                if (request is null)
                    return Results.BadRequest(new { message = "Payload inválido." });

                if (request.FromTeamId == Guid.Empty)
                    return Results.BadRequest(new { message = "Time de origem é obrigatório." });

                if (request.ToTeamId == Guid.Empty)
                    return Results.BadRequest(new { message = "Time de destino é obrigatório." });

                if (request.FromTeamId == request.ToTeamId)
                    return Results.BadRequest(new { message = "Informe times diferentes para a venda." });

                if (request.PlayerIds is null || request.PlayerIds.Length == 0)
                    return Results.BadRequest(new { message = "Selecione ao menos um jogador." });

                if (request.PlayerIds.Any(id => id == Guid.Empty))
                    return Results.BadRequest(new { message = "Jogador inválido na lista." });

                if (request.PlayerIds.Distinct().Count() != request.PlayerIds.Length)
                    return Results.BadRequest(new { message = "Não é permitido repetir jogadores." });

                if (request.Amount < 0m)
                    return Results.BadRequest(new { message = "O valor não pode ser negativo." });

                if (!EndpointHelpers.TryGetAdminToken(httpContext, out var adminToken, out var errorResult))
                    return errorResult!;

                try
                {
                    await adminTransferService.SellAsync(
                        adminToken!, request.FromTeamId, request.ToTeamId,
                        request.PlayerIds, request.Amount, request.Reason, ct);

                    return Results.Ok(new { message = "Venda concluída com sucesso." });
                }
                catch (AdminForbiddenException ex)
                {
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (AdminConflictException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { message = ex.Message });
                }
            });

            // SWAP
            adminTransferApi.MapPost("/swap", async (
                HttpContext httpContext,
                AdminSwapPlayersRequestDto request,
                AdminTransferService adminTransferService,
                CancellationToken ct) =>
            {
                if (request is null)
                    return Results.BadRequest(new { message = "Payload inválido." });

                if (request.TeamAId == Guid.Empty)
                    return Results.BadRequest(new { message = "Time A é obrigatório." });

                if (request.TeamBId == Guid.Empty)
                    return Results.BadRequest(new { message = "Time B é obrigatório." });

                if (request.TeamAId == request.TeamBId)
                    return Results.BadRequest(new { message = "Informe times diferentes para a troca." });

                var playersFromA = request.PlayersFromA ?? Array.Empty<Guid>();
                var playersFromB = request.PlayersFromB ?? Array.Empty<Guid>();

                if (playersFromA.Length == 0 && playersFromB.Length == 0)
                    return Results.BadRequest(new { message = "Selecione ao menos um jogador para a troca." });

                if (playersFromA.Any(id => id == Guid.Empty))
                    return Results.BadRequest(new { message = "Jogador inválido na lista do Time A." });

                if (playersFromB.Any(id => id == Guid.Empty))
                    return Results.BadRequest(new { message = "Jogador inválido na lista do Time B." });

                if (playersFromA.Distinct().Count() != playersFromA.Length)
                    return Results.BadRequest(new { message = "Não é permitido repetir jogadores do Time A." });

                if (playersFromB.Distinct().Count() != playersFromB.Length)
                    return Results.BadRequest(new { message = "Não é permitido repetir jogadores do Time B." });

                if (playersFromA.Intersect(playersFromB).Any())
                    return Results.BadRequest(new { message = "Um jogador não pode participar pelos dois times." });

                if (!EndpointHelpers.TryGetAdminToken(httpContext, out var adminToken, out var errorResult))
                    return errorResult!;

                try
                {
                    await adminTransferService.SwapAsync(
                        adminToken!, request.TeamAId, playersFromA,
                        request.TeamBId, playersFromB,
                        request.CashAdjustFromAToB, request.Reason, ct);

                    return Results.Ok(new { message = "Troca concluída com sucesso." });
                }
                catch (AdminForbiddenException ex)
                {
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (AdminConflictException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { message = ex.Message });
                }
            });

            adminTransferApi.MapPost("/move", async (
                HttpContext httpContext,
                AdminMovePlayerRequestDto request,
                AdminTransferService adminTransferService,
                CancellationToken ct) =>
            {
                if (request is null)
                    return Results.BadRequest(new { message = "Payload inválido." });

                if (request.PlayerId == Guid.Empty)
                    return Results.BadRequest(new { message = "Jogador é obrigatório." });

                if (request.ToTeamId == Guid.Empty)
                    return Results.BadRequest(new { message = "Time de destino é obrigatório." });

                if (!EndpointHelpers.TryGetAdminToken(httpContext, out var adminToken, out var errorResult))
                    return errorResult!;

                try
                {
                    await adminTransferService.MoveAsync(
                        adminToken!, request.PlayerId, request.ToTeamId, request.Reason, ct);

                    return Results.Ok(new { message = "Movimentação concluída com sucesso." });
                }
                catch (AdminForbiddenException ex)
                {
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (AdminConflictException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
                catch (KeyNotFoundException ex)
                {
                    return Results.NotFound(new { message = ex.Message });
                }
            });

            return app;
        }
    }
}
