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
            marketApi.MapMarketItemsEndpoints();

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
            marketApi.MapGet("/{itemId:guid}", async (
                Guid itemId,
                IMarketService marketService,
                IAuctionSettlementService settlementService,
                CancellationToken ct) =>
            {
                var item = await marketService.GetItemAsync(itemId, ct);
                return item is null
                    ? Results.NotFound(new { message = "Item não encontrado." })
                    : await ApplyPostSettlementRefreshAsync(item, marketService, settlementService, ct);
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
            adminMarketApi.MapMarketCycleEndpoints();

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

            adminMarketApi.MapPost("/close-expired", async (
                IMarketCycleService cycleService,
                IAuctionSettlementService settlementService,
                CancellationToken ct) =>
            {
                var cycle = await cycleService.ResolveAsync(null, ct).ConfigureAwait(false);
                if (cycle is null)
                {
                    return Results.NotFound(new { message = "Nenhum ciclo ativo encontrado." });
                }

                var summary = await settlementService.SettleExpiredItemsAsync(cycle.CycleId, ct).ConfigureAwait(false);
                return Results.Ok(new { cicloId = cycle.CycleId, vendidos = summary.Sold, expirados = summary.Expired });
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

        private static async Task<IResult> ApplyPostSettlementRefreshAsync(
            MarketItemDto item,
            IMarketService marketService,
            IAuctionSettlementService settlementService,
            CancellationToken ct)
        {
            var current = item;

            if (SettlementThrottle.TryAcquire())
            {
                var summary = await settlementService.SettleExpiredItemsAsync(item.CycleId, ct).ConfigureAwait(false);
                if (summary.Total > 0)
                {
                    var refreshed = await marketService.GetItemAsync(item.ItemId, ct).ConfigureAwait(false);
                    if (refreshed is not null)
                    {
                        current = refreshed;
                    }
                }
            }

            return Results.Ok(current);
        }

        record MarketBuyNowRequest(string? TeamToken);
    }
}
