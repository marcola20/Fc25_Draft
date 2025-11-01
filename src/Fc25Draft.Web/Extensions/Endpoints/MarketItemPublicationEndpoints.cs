using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Fc25Draft.Web.Extensions.Endpoints
{
    public static class MarketItemPublicationEndpoints
    {
        /// <summary>
        /// Mapeia /items dentro do grupo /market já existente.
        /// Uso esperado: marketApi.MapMarketItemPublicationEndpoints();
        /// </summary>
        public static RouteGroupBuilder MapMarketItemPublicationEndpoints(this RouteGroupBuilder marketApi)
        {
            var itemsApi = marketApi.MapGroup("/items")
                                    .RequireAuthorization("AdminOnly");

            itemsApi.MapGet("/drafts", async (IMarketItemPublicationService service, CancellationToken ct) =>
            {
                var items = await service.ListAsync(ct).ConfigureAwait(false);
                return Results.Ok(items);
            });

            itemsApi.MapGet("/{itemId:guid}", async (
                Guid itemId,
                HttpContext context,
                IMarketItemPublicationService service,
                CancellationToken ct) =>
            {
                var item = await service.GetAsync(itemId, ct).ConfigureAwait(false);
                if (item is null)
                {
                    var problem = new ProblemDetails
                    {
                        Title = "Item não encontrado.",
                        Detail = "O item solicitado não existe ou foi removido.",
                        Status = StatusCodes.Status404NotFound,
                        Type = "https://httpstatuses.com/404"
                    };
                    return Results.Problem(problem);
                }

                EndpointHelpers.ApplyEtag(context.Response, item.RowVersion);
                return Results.Ok(item);
            });

            itemsApi.MapPost(string.Empty, async (
                MarketItemDraftCreateRequest request,
                HttpContext context,
                IMarketItemPublicationService service,
                CancellationToken ct) =>
            {
                try
                {
                    var created = await service.CreateDraftAsync(request, ct).ConfigureAwait(false);
                    EndpointHelpers.ApplyEtag(context.Response, created.RowVersion);
                    return Results.Created($"/api/market/items/{created.ItemId}", created);
                }
                catch (MarketItemValidationException ex)
                {
                    return EndpointHelpers.CreateValidationProblem(ex.Message);
                }
                catch (MarketConflictException ex)
                {
                    return EndpointHelpers.CreateConflictProblem(ex.Message);
                }
            });

            itemsApi.MapPut("/{itemId:guid}", async (
                Guid itemId,
                MarketItemDraftUpdateRequest request,
                HttpContext context,
                IMarketItemPublicationService service,
                CancellationToken ct) =>
            {
                if (!EndpointHelpers.TryResolveRowVersion(context.Request, out var rowVersion, out var error))
                {
                    return error!;
                }

                try
                {
                    var updated = await service.UpdateDraftAsync(itemId, request, rowVersion, ct).ConfigureAwait(false);
                    EndpointHelpers.ApplyEtag(context.Response, updated.RowVersion);
                    return Results.Ok(updated);
                }
                catch (MarketItemValidationException ex)
                {
                    return EndpointHelpers.CreateValidationProblem(ex.Message);
                }
                catch (MarketNotFoundException ex)
                {
                    return EndpointHelpers.CreateNotFoundProblem(ex.Message);
                }
                catch (MarketConflictException ex)
                {
                    return EndpointHelpers.CreateConflictProblem(ex.Message);
                }
                catch (MarketPreconditionFailedException ex)
                {
                    return EndpointHelpers.CreatePreconditionFailedProblem(ex.Message);
                }
            });

            itemsApi.MapPost("/{itemId:guid}/publish", async (
                Guid itemId,
                HttpContext context,
                IMarketItemPublicationService service,
                CancellationToken ct) =>
            {
                if (!EndpointHelpers.TryResolveRowVersion(context.Request, out var rowVersion, out var error))
                {
                    return error!;
                }

                try
                {
                    var published = await service.PublishAsync(itemId, rowVersion, ct).ConfigureAwait(false);
                    EndpointHelpers.ApplyEtag(context.Response, published.RowVersion);
                    return Results.Ok(published);
                }
                catch (MarketNotFoundException ex)
                {
                    return EndpointHelpers.CreateNotFoundProblem(ex.Message);
                }
                catch (MarketConflictException ex)
                {
                    return EndpointHelpers.CreateConflictProblem(ex.Message);
                }
                catch (MarketPreconditionFailedException ex)
                {
                    return  EndpointHelpers.CreatePreconditionFailedProblem(ex.Message);
                }
            });

            itemsApi.MapDelete("/{itemId:guid}", async (
                Guid itemId,
                HttpContext context,
                IMarketItemPublicationService service,
                CancellationToken ct) =>
            {
                if (!EndpointHelpers.TryResolveRowVersion(context.Request, out var rowVersion, out var error))
                {
                    return error!;
                }

                try
                {
                    await service.SoftDeleteAsync(itemId, rowVersion, ct).ConfigureAwait(false);
                    EndpointHelpers.ApplyEtag(context.Response, rowVersion);
                    return Results.NoContent();
                }
                catch (MarketNotFoundException ex)
                {
                    return EndpointHelpers.CreateNotFoundProblem(ex.Message);
                }
                catch (MarketConflictException ex)
                {
                    return EndpointHelpers.CreateConflictProblem(ex.Message);
                }
                catch (MarketPreconditionFailedException ex)
                {
                    return EndpointHelpers.CreatePreconditionFailedProblem(ex.Message);
                }
            });

            return marketApi;
        }
    }
}
