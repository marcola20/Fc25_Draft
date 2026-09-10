using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;

namespace Fc25Draft.Web.Extensions.Endpoints;

public static class DraftWishlistEndpoints
{
    public static IEndpointRouteBuilder MapDraftWishlistEndpoints(this IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/draft/wishlist");

        api.MapGet("/edicoes", async (IDraftWishlistService service, CancellationToken ct) =>
            Results.Ok(await service.GetEdicoesAsync(ct)));

        api.MapGet(string.Empty, async (HttpContext httpContext, IDraftWishlistService service, int? versao, CancellationToken ct) =>
        {
            var token = ReadTeamToken(httpContext);
            if (token is null)
                return Results.Json(new { message = "Token do time obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);

            try
            {
                return Results.Ok(await service.GetByTokenAsync(token, versao, ct));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        api.MapPut(string.Empty, async (
            HttpContext httpContext,
            IDraftWishlistService service,
            DraftWishlistSaveRequestDto request,
            CancellationToken ct) =>
        {
            var token = ReadTeamToken(httpContext);
            if (token is null)
                return Results.Json(new { message = "Token do time obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);

            try
            {
                return Results.Ok(await service.SaveAsync(token, request?.PlayerIds ?? Array.Empty<int>(), ct));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
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

        var adminApi = routes.MapGroup("/admin/draft/wishlist").RequireAuthorization("AdminOnly");

        adminApi.MapGet(string.Empty, async (IDraftWishlistService service, int? versao, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await service.GetAllAsync(versao, ct));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        adminApi.MapGet("/votes", async (IDraftWishlistService service, int? versao, CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await service.GetVotesAsync(versao, ct));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        adminApi.MapPost("/edicoes", async (
            IDraftWishlistService service,
            DraftWishlistNovaEdicaoRequestDto? request,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await service.AbrirNovaEdicaoAsync(request?.Nome, ct));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        adminApi.MapPut("/edicoes/{numero:int}/status", async (
            IDraftWishlistService service,
            int numero,
            DraftWishlistEdicaoStatusRequestDto request,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await service.AlterarStatusEdicaoAsync(numero, request.Aberta, ct));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        });

        return routes;
    }

    private static string? ReadTeamToken(HttpContext httpContext)
    {
        var header = httpContext.Request.Headers["X-Team-Token"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header))
            return header.Trim();

        var authorization = httpContext.Request.Headers.Authorization.FirstOrDefault();
        const string bearer = "Bearer ";
        if (!string.IsNullOrWhiteSpace(authorization) &&
            authorization.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
        {
            var value = authorization[bearer.Length..].Trim();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return null;
    }
}
