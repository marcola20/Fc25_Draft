using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Web.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Endpoints.Transfers;

public static class TransferOffersEndpoints
{
    public static RouteGroupBuilder MapTransferOffersEndpoints(this RouteGroupBuilder transfersApi)
    {
        var offersApi = transfersApi.MapGroup("/offers");

        offersApi.MapPost(string.Empty, CreateOfferAsync);
        offersApi.MapGet("/received", QueryReceivedAsync);
        offersApi.MapGet("/sent", QuerySentAsync);
        offersApi.MapGet("/{offerId:guid}", GetOfferByIdAsync);
        offersApi.MapPost("/{offerId:guid}/accept", AcceptOfferAsync);
        offersApi.MapPost("/{offerId:guid}/reject", RejectOfferAsync);
        offersApi.MapPost("/{offerId:guid}/withdraw", WithdrawOfferAsync);
        offersApi.MapPost("/{offerId:guid}/counter", CounterOfferAsync);

        return transfersApi;
    }

    private static async Task<IResult> CreateOfferAsync(
        CreateTransferOfferRequest request,
        HttpContext http,
        DraftDbContext db,
        ITransferOfferService offerService,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Payload inválido." });
        }

        if (!await TryResolveTeamAsync(http, db, ct, out var team, out var errorResult).ConfigureAwait(false))
        {
            return errorResult!;
        }

        try
        {
            var created = await offerService.CreateOfferAsync(
                team!.Value.TeamId,
                request.ToTeamId,
                request.PlayerId,
                request.OfferedFee,
                request.SwapPlayerIds ?? Array.Empty<int>(),
                request.Message,
                request.ExpiresAtUtc,
                ct).ConfigureAwait(false);

            var detail = await ProjectDetailAsync(db, created.OfferId, ct).ConfigureAwait(false);

            if (detail is null)
            {
                return Results.Created($"/api/transfers/offers/{created.OfferId}", new { offerId = created.OfferId });
            }

            ApplyConcurrencyHeaders(http.Response, detail.RowVersion);

            return Results.Created($"/api/transfers/offers/{detail.OfferId}", detail);
        }
        catch (ArgumentException ex)
        {
            return EndpointHelpers.CreateValidationProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperationToResult(ex);
        }
    }

    private static async Task<IResult> QueryReceivedAsync(
        HttpContext http,
        DraftDbContext db,
        CancellationToken ct)
    {
        if (!await TryResolveTeamAsync(http, db, ct, out var team, out var errorResult).ConfigureAwait(false))
        {
            return errorResult!;
        }

        var offers = await db.TransferOffers
            .AsNoTracking()
            .Where(o => o.ToTeamId == team!.Value.TeamId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(ProjectSummary())
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return Results.Ok(offers);
    }

    private static async Task<IResult> QuerySentAsync(
        HttpContext http,
        DraftDbContext db,
        CancellationToken ct)
    {
        if (!await TryResolveTeamAsync(http, db, ct, out var team, out var errorResult).ConfigureAwait(false))
        {
            return errorResult!;
        }

        var offers = await db.TransferOffers
            .AsNoTracking()
            .Where(o => o.FromTeamId == team!.Value.TeamId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Select(ProjectSummary())
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return Results.Ok(offers);
    }

    private static async Task<IResult> GetOfferByIdAsync(
        Guid offerId,
        HttpContext http,
        DraftDbContext db,
        CancellationToken ct)
    {
        if (offerId == Guid.Empty)
        {
            return EndpointHelpers.CreateValidationProblem("Identificador de proposta inválido.");
        }

        if (!await TryResolveTeamAsync(http, db, ct, out var team, out var errorResult).ConfigureAwait(false))
        {
            return errorResult!;
        }

        var detail = await db.TransferOffers
            .AsNoTracking()
            .Where(o => o.OfferId == offerId && (o.FromTeamId == team!.Value.TeamId || o.ToTeamId == team!.Value.TeamId))
            .Select(ProjectDetail())
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return detail is null
            ? EndpointHelpers.CreateNotFoundProblem("Oferta não encontrada.")
            : Results.Ok(detail);
    }

    private static async Task<IResult> AcceptOfferAsync(
        Guid offerId,
        HttpContext http,
        DraftDbContext db,
        ITransferOfferService offerService,
        CancellationToken ct)
    {
        if (!await TryResolveTeamAsync(http, db, ct, out var team, out var errorResult).ConfigureAwait(false))
        {
            return errorResult!;
        }

        if (!EndpointHelpers.TryResolveRowVersion(http.Request, out var rowVersion, out var rowError, allowFallbackToRowVersionHeader: true))
        {
            return rowError!;
        }

        var snapshot = await LoadOfferSnapshotAsync(offerId, db, ct).ConfigureAwait(false);
        if (snapshot is null)
        {
            return EndpointHelpers.CreateNotFoundProblem("Oferta não encontrada.");
        }

        if (snapshot.Value.ToTeamId != team!.Value.TeamId)
        {
            return Results.Json(new { message = "Apenas o time destinatário pode aceitar a proposta." }, statusCode: StatusCodes.Status403Forbidden);
        }

        if (snapshot.Value.Status != TransferOfferStatus.Pending)
        {
            return EndpointHelpers.CreateConflictProblem("A oferta não está mais pendente.");
        }

        try
        {
            await offerService.AcceptOfferAsync(offerId, rowVersion, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return EndpointHelpers.CreateValidationProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperationToResult(ex);
        }

        var detail = await ProjectDetailAsync(db, offerId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return EndpointHelpers.CreateNotFoundProblem("Oferta não encontrada.");
        }

        ApplyConcurrencyHeaders(http.Response, detail.RowVersion);
        return Results.Ok(detail);
    }

    private static async Task<IResult> RejectOfferAsync(
        Guid offerId,
        RejectTransferOfferRequest request,
        HttpContext http,
        DraftDbContext db,
        ITransferOfferService offerService,
        CancellationToken ct)
    {
        if (!await TryResolveTeamAsync(http, db, ct, out var team, out var errorResult).ConfigureAwait(false))
        {
            return errorResult!;
        }

        if (!EndpointHelpers.TryResolveRowVersion(http.Request, out var rowVersion, out var rowError, allowFallbackToRowVersionHeader: true))
        {
            return rowError!;
        }

        var snapshot = await LoadOfferSnapshotAsync(offerId, db, ct).ConfigureAwait(false);
        if (snapshot is null)
        {
            return EndpointHelpers.CreateNotFoundProblem("Oferta não encontrada.");
        }

        if (snapshot.Value.ToTeamId != team!.Value.TeamId)
        {
            return Results.Json(new { message = "Apenas o time destinatário pode rejeitar a proposta." }, statusCode: StatusCodes.Status403Forbidden);
        }

        if (snapshot.Value.Status != TransferOfferStatus.Pending)
        {
            return EndpointHelpers.CreateConflictProblem("A oferta não está mais pendente.");
        }

        try
        {
            await offerService.RejectOfferAsync(offerId, request?.ResponseMessage, rowVersion, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return EndpointHelpers.CreateValidationProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperationToResult(ex);
        }

        var detail = await ProjectDetailAsync(db, offerId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return EndpointHelpers.CreateNotFoundProblem("Oferta não encontrada.");
        }

        ApplyConcurrencyHeaders(http.Response, detail.RowVersion);
        return Results.Ok(detail);
    }

    private static async Task<IResult> WithdrawOfferAsync(
        Guid offerId,
        HttpContext http,
        DraftDbContext db,
        ITransferOfferService offerService,
        CancellationToken ct)
    {
        if (!await TryResolveTeamAsync(http, db, ct, out var team, out var errorResult).ConfigureAwait(false))
        {
            return errorResult!;
        }

        if (!EndpointHelpers.TryResolveRowVersion(http.Request, out var rowVersion, out var rowError, allowFallbackToRowVersionHeader: true))
        {
            return rowError!;
        }

        var snapshot = await LoadOfferSnapshotAsync(offerId, db, ct).ConfigureAwait(false);
        if (snapshot is null)
        {
            return EndpointHelpers.CreateNotFoundProblem("Oferta não encontrada.");
        }

        if (snapshot.Value.FromTeamId != team!.Value.TeamId)
        {
            return Results.Json(new { message = "Apenas o time ofertante pode retirar a proposta." }, statusCode: StatusCodes.Status403Forbidden);
        }

        if (snapshot.Value.Status != TransferOfferStatus.Pending)
        {
            return EndpointHelpers.CreateConflictProblem("A oferta não está mais pendente.");
        }

        try
        {
            await offerService.CancelOfferAsync(offerId, team.Value.TeamId, rowVersion, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return EndpointHelpers.CreateValidationProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperationToResult(ex);
        }

        var detail = await ProjectDetailAsync(db, offerId, ct).ConfigureAwait(false);
        if (detail is null)
        {
            return EndpointHelpers.CreateNotFoundProblem("Oferta não encontrada.");
        }

        ApplyConcurrencyHeaders(http.Response, detail.RowVersion);
        return Results.Ok(detail);
    }

    private static async Task<IResult> CounterOfferAsync(
        Guid offerId,
        CounterTransferOfferRequest request,
        HttpContext http,
        DraftDbContext db,
        ITransferOfferService offerService,
        CancellationToken ct)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Payload inválido." });
        }

        if (!await TryResolveTeamAsync(http, db, ct, out var team, out var errorResult).ConfigureAwait(false))
        {
            return errorResult!;
        }

        if (!EndpointHelpers.TryResolveRowVersion(http.Request, out var rowVersion, out var rowError, allowFallbackToRowVersionHeader: true))
        {
            return rowError!;
        }

        var snapshot = await LoadOfferSnapshotAsync(offerId, db, ct).ConfigureAwait(false);
        if (snapshot is null)
        {
            return EndpointHelpers.CreateNotFoundProblem("Oferta não encontrada.");
        }

        if (snapshot.Value.FromTeamId != team!.Value.TeamId)
        {
            return Results.Json(new { message = "Apenas o time ofertante pode enviar uma contraproposta." }, statusCode: StatusCodes.Status403Forbidden);
        }

        if (snapshot.Value.Status != TransferOfferStatus.Pending)
        {
            return EndpointHelpers.CreateConflictProblem("A oferta não está mais pendente.");
        }

        try
        {
            await offerService.CancelOfferAsync(offerId, team.Value.TeamId, rowVersion, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return EndpointHelpers.CreateValidationProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperationToResult(ex);
        }

        TransferOfferDetailDto? detail;

        try
        {
            var created = await offerService.CreateOfferAsync(
                team.Value.TeamId,
                snapshot.Value.ToTeamId,
                snapshot.Value.PlayerId,
                request.OfferedFee,
                request.SwapPlayerIds ?? Array.Empty<int>(),
                request.Message,
                request.ExpiresAtUtc,
                ct).ConfigureAwait(false);

            detail = await ProjectDetailAsync(db, created.OfferId, ct).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return EndpointHelpers.CreateValidationProblem(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return MapInvalidOperationToResult(ex);
        }

        if (detail is null)
        {
            return Results.Created($"/api/transfers/offers", new { message = "Contraproposta criada." });
        }

        ApplyConcurrencyHeaders(http.Response, detail.RowVersion);
        return Results.Created($"/api/transfers/offers/{detail.OfferId}", detail);
    }

    private static Func<TransferOffer, TransferOfferSummaryDto> ProjectSummary()
        => offer => new TransferOfferSummaryDto(
            offer.OfferId,
            offer.Status,
            offer.FromTeamId,
            offer.FromTeam.TeamName,
            offer.ToTeamId,
            offer.ToTeam.TeamName,
            new TransferOfferParticipantDto(
                offer.PlayerId,
                offer.Player.PlayerGuid,
                offer.Player.Name,
                offer.Player.Position.Name,
                offer.Player.Overall),
            offer.OfferedFee,
            offer.CreatedAtUtc,
            offer.UpdatedAtUtc,
            offer.ExpiresAtUtc,
            offer.RespondedAtUtc,
            offer.RowVersion,
            offer.SwapPlayers
                .OrderBy(sp => sp.Player.Name)
                .Select(sp => new TransferOfferSwapPlayerDto(
                    sp.PlayerId,
                    sp.Player.PlayerGuid,
                    sp.Player.Name,
                    sp.Player.Position.Name,
                    sp.Player.Overall))
                .ToList());

    private static Func<TransferOffer, TransferOfferDetailDto> ProjectDetail()
        => offer => new TransferOfferDetailDto(
            offer.OfferId,
            offer.Status,
            offer.FromTeamId,
            offer.FromTeam.TeamName,
            offer.ToTeamId,
            offer.ToTeam.TeamName,
            new TransferOfferParticipantDto(
                offer.PlayerId,
                offer.Player.PlayerGuid,
                offer.Player.Name,
                offer.Player.Position.Name,
                offer.Player.Overall),
            offer.OfferedFee,
            offer.Message,
            offer.ResponseMessage,
            offer.CreatedAtUtc,
            offer.UpdatedAtUtc,
            offer.ExpiresAtUtc,
            offer.RespondedAtUtc,
            offer.RowVersion,
            offer.SwapPlayers
                .OrderBy(sp => sp.Player.Name)
                .Select(sp => new TransferOfferSwapPlayerDto(
                    sp.PlayerId,
                    sp.Player.PlayerGuid,
                    sp.Player.Name,
                    sp.Player.Position.Name,
                    sp.Player.Overall))
                .ToList());

    private static async Task<TransferOfferDetailDto?> ProjectDetailAsync(DraftDbContext db, Guid offerId, CancellationToken ct)
        => await db.TransferOffers
            .AsNoTracking()
            .Where(o => o.OfferId == offerId)
            .Select(ProjectDetail())
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

    private static async Task<(Guid OfferId, Guid FromTeamId, Guid ToTeamId, int PlayerId, TransferOfferStatus Status)?> LoadOfferSnapshotAsync(
        Guid offerId,
        DraftDbContext db,
        CancellationToken ct)
    {
        if (offerId == Guid.Empty)
        {
            return null;
        }

        return await db.TransferOffers
            .AsNoTracking()
            .Where(o => o.OfferId == offerId)
            .Select(o => new ValueTuple<Guid, Guid, Guid, int, TransferOfferStatus>(
                o.OfferId,
                o.FromTeamId,
                o.ToTeamId,
                o.PlayerId,
                o.Status))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    private static void ApplyConcurrencyHeaders(HttpResponse response, uint rowVersion)
    {
        EndpointHelpers.ApplyEtag(response, rowVersion);
        response.Headers["X-RowVersion"] = rowVersion.ToString();
        response.Headers["X-Server-Time-Utc"] = DateTime.UtcNow.ToString("O");
    }

    private static IResult MapInvalidOperationToResult(InvalidOperationException ex)
    {
        var message = ex.Message ?? string.Empty;

        if (message.Contains("não encontrada", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointHelpers.CreateNotFoundProblem(message);
        }

        if (message.Contains("atualizada por outro processo", StringComparison.OrdinalIgnoreCase))
        {
            return EndpointHelpers.CreatePreconditionFailedProblem(message);
        }

        return EndpointHelpers.CreateConflictProblem(message);
    }

    private static async Task<bool> TryResolveTeamAsync(
        HttpContext http,
        DraftDbContext db,
        CancellationToken ct,
        out (Guid TeamId, string TeamName)? team,
        out IResult? error)
    {
        team = null;
        error = null;

        var token = http.Request.Headers["X-Team-Token"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token))
        {
            error = Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);
            return false;
        }

        var normalized = token.Trim();

        var identity = await db.Teams
            .AsNoTracking()
            .Where(t => t.Token == normalized)
            .Select(t => new { t.TeamId, t.TeamName })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (identity is null)
        {
            error = Results.Json(new { message = "Token inválido." }, statusCode: StatusCodes.Status403Forbidden);
            return false;
        }

        team = new(identity.TeamId, identity.TeamName);
        return true;
    }

    private sealed record CreateTransferOfferRequest(
        Guid ToTeamId,
        int PlayerId,
        decimal? OfferedFee,
        IReadOnlyList<int>? SwapPlayerIds,
        string? Message,
        DateTime? ExpiresAtUtc);

    private sealed record RejectTransferOfferRequest(string? ResponseMessage);

    private sealed record CounterTransferOfferRequest(
        decimal? OfferedFee,
        IReadOnlyList<int>? SwapPlayerIds,
        string? Message,
        DateTime? ExpiresAtUtc);
}
