using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Extensions.Endpoints;

public static class TransferOffersEndpoints
{
    public static IEndpointRouteBuilder MapTransferOffersEndpoints(this IEndpointRouteBuilder api)
    {
        var offersApi = api.MapGroup("/offers");

        offersApi.MapPost("/", HandleCreateAsync);
        offersApi.MapPost("/{offerId:guid}/respond", HandleRespondAsync);
        offersApi.MapPost("/{offerId:guid}/cancel", HandleCancelAsync);
        offersApi.MapGet("/received", HandleGetReceivedAsync);
        offersApi.MapGet("/sent", HandleGetSentAsync);
        offersApi.MapGet("/finished", HandleGetFinishedAsync);
        offersApi.MapGet("/{offerId:guid}", HandleGetByIdAsync);

        return api;
    }

    private static async Task<IResult> HandleCreateAsync(
        CreateTransferOfferDto dto,
        ITransferOfferService service,
        DraftDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var teamId = await ResolveTeamIdAsync(httpContext, db, ct);
        if (teamId is null)
            return Results.Unauthorized();

        if (dto.FromTeamId != teamId.Value)
            return Results.Forbid();

        try
        {
            var result = await service.CreateOfferAsync(dto, ct);
            return Results.Created($"/api/offers/{result.OfferId}", result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> HandleRespondAsync(
        Guid offerId,
        RespondToOfferDto dto,
        ITransferOfferService service,
        DraftDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var teamId = await ResolveTeamIdAsync(httpContext, db, ct);
        if (teamId is null)
            return Results.Unauthorized();

        try
        {
            var result = await service.RespondToOfferAsync(offerId, teamId.Value, dto.Response, ct);
            return Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> HandleCancelAsync(
        Guid offerId,
        ITransferOfferService service,
        DraftDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var teamId = await ResolveTeamIdAsync(httpContext, db, ct);
        if (teamId is null)
            return Results.Unauthorized();

        try
        {
            var result = await service.CancelOfferAsync(offerId, teamId.Value, ct);
            return Results.Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static async Task<IResult> HandleGetReceivedAsync(
        ITransferOfferService service,
        DraftDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var teamId = await ResolveTeamIdAsync(httpContext, db, ct);
        if (teamId is null)
            return Results.Unauthorized();

        var result = await service.GetReceivedOffersAsync(teamId.Value, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetSentAsync(
        ITransferOfferService service,
        DraftDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var teamId = await ResolveTeamIdAsync(httpContext, db, ct);
        if (teamId is null)
            return Results.Unauthorized();

        var result = await service.GetSentOffersAsync(teamId.Value, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetFinishedAsync(
        ITransferOfferService service,
        DraftDbContext db,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var teamId = await ResolveTeamIdAsync(httpContext, db, ct);
        if (teamId is null)
            return Results.Unauthorized();

        var result = await service.GetFinishedOffersAsync(teamId.Value, ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetByIdAsync(
        Guid offerId,
        ITransferOfferService service,
        CancellationToken ct)
    {
        var result = await service.GetByIdAsync(offerId, ct);
        return result is null
            ? Results.NotFound(new { message = "Proposta não encontrada." })
            : Results.Ok(result);
    }

    private static async Task<Guid?> ResolveTeamIdAsync(HttpContext httpContext, DraftDbContext db, CancellationToken ct)
    {
        var tokenHeader = httpContext.Request.Headers["X-Team-Token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tokenHeader))
            return null;

        var normalized = tokenHeader.Trim();
        var team = await db.Teams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == normalized || t.AuxToken == normalized, ct);

        return team?.TeamId;
    }
}
