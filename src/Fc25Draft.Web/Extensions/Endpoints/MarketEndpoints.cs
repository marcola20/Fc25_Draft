using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Services;
using Fc25Draft.Web.Endpoints.Market;
using Microsoft.AspNetCore.Mvc;

namespace Fc25Draft.Web.Extensions.Endpoints
{
    public static class MarketEndpoints
    {
        public static IEndpointRouteBuilder MapMarketEndpoints(this IEndpointRouteBuilder api)
        {
            var marketApi = api.MapGroup("/market");

            marketApi.MapMarketItemPublicationEndpoints();
            marketApi.MapMarketCycleEndpoints();

            // GET /api/market
            marketApi.MapGet(string.Empty, async (IMarketService market, ILoggerFactory lf, IWebHostEnvironment env, HttpContext http, CancellationToken ct) =>
            {
                var log = lf.CreateLogger("MarketList");

                try
                {
                    var items = await market.GetActiveItemsAsync(ct);
                    http.Response.Headers["x-server-time-utc"] = DateTime.UtcNow.ToString("O");
                    return Results.Ok(items);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "❌ Erro interno em GET /api/market: {Message}", ex.Message);

                    var problem = new ProblemDetails
                    {
                        Title = "Failed to load market items.",
                        Detail = env.IsDevelopment() ? ex.ToString() : ex.Message,
                        Status = StatusCodes.Status500InternalServerError
                    };

                    return Results.Problem(
                        title: problem.Title,
                        detail: problem.Detail,
                        statusCode: problem.Status);
                }
            }).AllowAnonymous();


            // POST /api/market/history  (Admin)
            marketApi.MapPost("/history", async (RegisterTransferHistoryRequestDto request, ITransferHistoryService transferHistoryService) =>
            {
                if (request is null)
                {
                    return Results.BadRequest(new { message = "Payload inválido." });
                }

                var entry = new TransferHistory
                {
                    TransferId = request.TransferId ?? Guid.Empty,
                    PlayerId = request.PlayerId,
                    FromTeamId = request.FromTeamId,
                    ToTeamId = request.ToTeamId,
                    Amount = request.Amount,
                    Type = request.Type,
                    Notes = request.Notes,
                    PerformedBy = request.PerformedBy,
                    PerformedAtUtc = request.PerformedAtUtc ?? default
                };

                try
                {
                    await transferHistoryService.RegisterTransferAsync(entry);

                    var saved = (await transferHistoryService.GetRecentTransfersAsync(1))
                        .FirstOrDefault(h => h.TransferId == entry.TransferId);

                    var result = saved is not null? EndpointHelpers.MapTransferHistoryToDto(saved) : EndpointHelpers.MapTransferHistoryToDto(entry);

                    return Results.Created($"/api/market/history/{entry.TransferId}", result);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            }).RequireAuthorization("AdminOnly");

            // GET /api/market/{itemId}
            marketApi.MapGet("/{itemId:guid}", async (Guid itemId, IMarketService marketService, CancellationToken ct) =>
            {
                await marketService.CloseExpiredItemsAsync(ct);
                var item = await marketService.GetItemAsync(itemId, ct);
                return item is null
                    ? Results.NotFound(new { message = "Item não encontrado." })
                    : Results.Ok(item);
            }).AllowAnonymous();

            // POST /api/market/{itemId}/bid
            marketApi.MapPost("/{itemId:guid}/bid", async (
                Guid itemId,
                MarketBidRequest request,
                HttpContext context,
                IMarketService marketService,
                CancellationToken ct) =>
            {
                var token = GetTeamToken(context, request.TeamToken);
                if (token is null)
                {
                    return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);
                }

            if (!EndpointHelpers.TryResolveRowVersion(context.Request, out var rowVersion, out var errorResult, allowFallbackToRowVersionHeader: true))
                {
                    return errorResult!;
                }

                try
                {
                    var result = await marketService.PlaceBidAsync(itemId, token, request.Amount, rowVersion, ct);
                    return Results.Ok(result);
                }
                catch (MarketForbiddenException ex)
                {
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
                }
                catch (MarketValidationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (MarketConflictException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
                catch (MarketPreconditionFailedException ex)
                {
                    return EndpointHelpers.CreatePreconditionFailedProblem(ex.Message);
                }
                catch (MarketNotFoundException ex)
                {
                    return Results.NotFound(new { message = ex.Message });
                }
            }).AllowAnonymous();

            // POST /api/market/{itemId}/buy-now
            marketApi.MapPost("/{itemId:guid}/buy-now", async (
                Guid itemId,
                MarketBuyNowRequest request,
                HttpContext context,
                IMarketService marketService,
                CancellationToken ct) =>
            {
                var token = GetTeamToken(context, request.TeamToken);
                if (token is null)
                {
                    return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);
                }

                if (!EndpointHelpers.TryResolveRowVersion(context.Request, out var rowVersion, out var errorResult, allowFallbackToRowVersionHeader: true))
                {
                    return errorResult!;
                }

                try
                {
                    var result = await marketService.BuyNowAsync(itemId, token, rowVersion, ct);
                    return Results.Ok(result);
                }
                catch (MarketForbiddenException ex)
                {
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
                }
                catch (MarketValidationException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
                catch (MarketConflictException ex)
                {
                    return Results.Conflict(new { message = ex.Message });
                }
                catch (MarketPreconditionFailedException ex)
                {
                    return EndpointHelpers.CreatePreconditionFailedProblem(ex.Message);
                }
                catch (MarketNotFoundException ex)
                {
                    return Results.NotFound(new { message = ex.Message });
                }
            }).AllowAnonymous();

            // ADMIN /api/admin/market
            var adminMarketApi = api.MapGroup("/admin/market").RequireAuthorization("AdminOnly");

            adminMarketApi.MapPost("/refresh", async (IMarketCycleGenerator cycleGenerator, CancellationToken ct) =>
            {
                var now = DateTime.UtcNow;
                var needsNew = await cycleGenerator.NeedsNewCycleAsync(now, ct);
                if (!needsNew)
                {
                    return Results.Conflict(new { message = "Já existe um ciclo ativo." });
                }

                var cycle = await cycleGenerator.CreateNewCycleAsync(now, ct);
                return Results.Ok(cycle);
            });

            adminMarketApi.MapPost("/close-expired", async (IMarketService marketService, CancellationToken ct) =>
            {
                var closed = await marketService.CloseExpiredItemsAsync(ct);
                return Results.Ok(new { itensFechados = closed });
            });

            adminMarketApi.MapPost("/cancel/{itemId:guid}", async (
                Guid itemId,
                AdminCancelMarketItemRequestDto request,
                HttpContext httpContext,
                AdminTransferService adminTransferService,
                CancellationToken ct) =>
            {
                if (request is null)
                {
                    return Results.BadRequest(new { message = "Payload inválido." });
                }

                if (!EndpointHelpers.TryGetAdminToken(httpContext, out var adminToken, out var errorResult))
                {
                    return errorResult!;
                }

                try
                {
                    await adminTransferService.CancelMarketItemAsync(adminToken!, itemId, request.Reason, ct);
                    return Results.Ok(new { message = "Item cancelado com sucesso." });
                }
                catch (AdminForbiddenException ex)
                {
                    return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
                }
                catch (ArgumentException ex)
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

            return api;
        }

        private static string? GetTeamToken(HttpContext context, string? payloadToken)
        {
            var headerToken = context.Request.Headers["X-Team-Token"].FirstOrDefault();
            var token = !string.IsNullOrWhiteSpace(payloadToken) ? payloadToken : headerToken;
            return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        }

        record MarketBidRequest(decimal Amount, string? TeamToken);

        record MarketBuyNowRequest(string? TeamToken);
    }
}
