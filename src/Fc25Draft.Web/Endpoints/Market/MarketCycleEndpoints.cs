using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Web.Models.MarketCycles;
using Fc25Draft.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Web.Endpoints.Market;

public static class MarketCycleEndpoints
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";
    private static readonly TimeSpan StatusUpdateIdempotencyTtl = TimeSpan.FromMinutes(2);

    public static RouteGroupBuilder MapMarketCycleEndpoints(this RouteGroupBuilder marketApi)
    {
        var cycles = marketApi.MapGroup("/cycles").RequireAuthorization("AdminOnly");
        cycles.MapPost("", HandleCreateAsync);
        cycles.MapGet("", HandleQueryAsync);
        cycles.MapGet("/{cycleId:guid}", HandleGetAsync);
        cycles.MapPatch("/{cycleId:guid}/status", HandleStatusUpdateAsync);
        cycles.MapPost("/{cycleId:guid}:conclude", HandleConcludeAsync);
        cycles.MapPost("/{cycleId:guid}/items/preview", HandlePreviewAsync);
        cycles.MapPost("/{cycleId:guid}/items/generate", HandleGenerateAsync);
        cycles.MapDelete("/{cycleId:guid}/items", HandleDeleteItemsAsync);
        return marketApi;
    }

    private static async Task<IResult> HandleCreateAsync(
        MarketCycleCreateRequest request,
        IMarketCycleAdminService service,
        CancellationToken ct)
    {
        if (!TryValidate(request, out var validationError))
            return validationError!;

        if (!request.StartsAtUtc.HasValue || !request.EndsAtUtc.HasValue)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(request.StartsAtUtc)] = new[] { "A data de início é obrigatória." },
                [nameof(request.EndsAtUtc)] = new[] { "A data de término é obrigatória." }
            }, statusCode: StatusCodes.Status400BadRequest, title: "Falha na validação.");
        }

        var command = new MarketCycleCreateCommand(
            request.Name!.Trim(),
            EnsureUtc(request.StartsAtUtc.Value),
            EnsureUtc(request.EndsAtUtc.Value),
            request.Status,
            string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim());

        try
        {
            var created = await service.CreateAsync(command, ct).ConfigureAwait(false);
            return TypedResults.Created($"/api/admin/market/cycles/{created.CycleId}", created);
        }
        catch (MarketConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (MarketValidationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (DbUpdateException) 
        {
            return Results.Problem("Falha ao salvar o ciclo.", statusCode: StatusCodes.Status500InternalServerError);
        }
    }


    private static async Task<IResult> HandleQueryAsync(
        [AsParameters] MarketCycleQueryRequest request,
        IMarketCycleAdminService service,
        CancellationToken ct)
    {
        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        var command = new MarketCycleQuery(
            request.Page,
            request.PageSize,
            request.Status,
            request.StartsAfterUtc,
            request.StartsBeforeUtc);

        var result = await service.QueryAsync(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetAsync(
        Guid cycleId,
        IMarketCycleAdminService service,
        CancellationToken ct)
    {
        var cycle = await service.GetByIdAsync(cycleId, ct).ConfigureAwait(false);
        return cycle is null
            ? Results.NotFound(new { message = "Ciclo não encontrado." })
            : Results.Ok(cycle);
    }

    private static async Task<IResult> HandleStatusUpdateAsync(
    [FromRoute] Guid cycleId,
    [FromBody] MarketCycleStatusUpdateRequest request,
    [FromServices] IMarketCycleAdminService service,
    [FromServices] IAuctionSettlementService settlementService,
    [FromServices] IIdempotencyStore idempotencyStore,
    [FromServices] ILoggerFactory loggerFactory,
    HttpContext httpContext,
    CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("MarketCycleEndpoints");

        if (!TryValidate(request, out var validationError))
            return validationError!;

        var status = request.Status ?? MarketCycleStatus.Draft;
        var idempotencyKey = ExtractIdempotencyKey(httpContext);

        if (!string.IsNullOrEmpty(idempotencyKey) &&
            !idempotencyStore.TryRegister(idempotencyKey, StatusUpdateIdempotencyTtl))
        {
            logger.LogInformation(
                "Ignoring duplicate status update for cycle {CycleId} to {Status} with key {IdempotencyKey}",
                cycleId, status, idempotencyKey);
            return TypedResults.AcceptedAtRoute();
        }

        try
        {
            var updated = await service.UpdateStatusAsync(cycleId, status, request.ForceClose, ct);
            logger.LogInformation("Updated cycle {CycleId} to {Status} with key {IdempotencyKey}",
                cycleId, updated.Status, idempotencyKey);

            if (updated.Status == MarketCycleStatus.Closed)
            {
                var summary = await settlementService.SettleAllOpenItemsOnCycleCloseAsync(cycleId, request.ForceClose, ct);
                var payload = new MarketCycleStatusUpdateResult(
                    updated,
                    new MarketCycleSettlementSummary(summary.Sold, summary.Expired, summary.Canceled));
                return TypedResults.Ok(payload);
            }

            return TypedResults.Ok(new MarketCycleStatusUpdateResult(updated, null));
        }
        catch (MarketNotFoundException ex) { return Results.NotFound(new { message = ex.Message }); }
        catch (MarketConflictException ex) { return Results.Conflict(new { message = ex.Message }); }
        catch (MarketValidationException ex) { return Results.Conflict(new { message = ex.Message }); }
    }

    private static async Task<IResult> HandleConcludeAsync(
        Guid cycleId,
        IMarketCycleAdminService service,
        CancellationToken ct)
    {
        try
        {
            var result = await service.ConcludeAsync(cycleId, ct).ConfigureAwait(false);
            return TypedResults.Ok(result);
        }
        catch (MarketNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (MarketConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
    }

    private static string? ExtractIdempotencyKey(HttpContext httpContext)
    {
        if (!httpContext.Request.Headers.TryGetValue(IdempotencyKeyHeaderName, out var values))
        {
            return null;
        }

        var key = values.ToString();
        return string.IsNullOrWhiteSpace(key) ? null : key.Trim();
    }

    private static async Task<IResult> HandlePreviewAsync(
        Guid cycleId,
        MarketItemGenerationRequestDto request,
        IMarketItemGenerationService service,
        CancellationToken ct)
    {
        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        try
        {
            var options = ToOptions(request);
            var preview = await service.PreviewAsync(cycleId, options, ct).ConfigureAwait(false);
            return Results.Ok(ToDto(preview));
        }
        catch (MarketNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (MarketValidationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> HandleGenerateAsync(
        Guid cycleId,
        MarketItemGenerationRequestDto request,
        IMarketItemGenerationService service,
        CancellationToken ct)
    {
        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        try
        {
            var options = ToOptions(request);
            var result = await service.GenerateAsync(cycleId, options, ct).ConfigureAwait(false);
            return Results.Ok(ToDto(result));
        }
        catch (MarketNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (MarketValidationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static async Task<IResult> HandleDeleteItemsAsync(
        Guid cycleId,
        IMarketItemGenerationService service,
        CancellationToken ct)
    {
        try
        {
            var removed = await service.DeleteDraftsAsync(cycleId, ct).ConfigureAwait(false);
            return Results.Ok(new MarketItemGenerationDeleteResultDto(removed));
        }
        catch (MarketNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (MarketValidationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }

    private static MarketItemGenerationOptions ToOptions(MarketItemGenerationRequestDto request)
    {
        var positions = request.PositionIds?.Distinct().ToArray();

        return new MarketItemGenerationOptions(
            request.DesiredCount,
            positions is { Length: > 0 } ? positions : null,
            request.MinOverall,
            request.MaxOverall,
            request.MinAge,
            request.MaxAge,
            request.MaxPerTeam,
            request.MaxPerPosition,
            request.ExcludeAlreadyListedInOpenCycles,
            request.EnsureUniquePlayerPerCycle,
            request.Seed,
            request.MinItemLifespan,
            request.MaxItemLifespan,
            request.AutoSpreadExpirationsAcrossCycle);
    }

    private static MarketItemGenerationPreviewDto ToDto(MarketItemGenerationPreview preview)
    {
        return new MarketItemGenerationPreviewDto(
            preview.RequestedCount,
            preview.EligibleCount,
            preview.Seed,
            preview.Items.Select(ToDto).ToList(),
            preview.Skipped.Select(ToDto).ToList(),
            preview.FirstExpirationUtc,
            preview.LastExpirationUtc);
    }

    private static MarketItemGenerationResultDto ToDto(MarketItemGenerationResult result)
    {
        return new MarketItemGenerationResultDto(
            result.RequestedCount,
            result.EligibleCount,
            result.Seed,
            result.CreatedCount,
            result.Items.Select(ToDto).ToList(),
            result.Skipped.Select(ToDto).ToList(),
            result.FirstExpirationUtc,
            result.LastExpirationUtc);
    }

    private static MarketItemGenerationItemDto ToDto(MarketItemGenerationItem item)
    {
        return new MarketItemGenerationItemDto(
            item.PlayerId,
            item.PlayerName,
            item.PositionId,
            item.PositionName,
            item.Overall,
            item.Age,
            item.TeamId,
            item.TeamName,
            item.BasePrice,
            item.BuyNowPrice,
            item.MinIncrement,
            item.ExpiresAtUtc);
    }

    private static MarketItemGenerationSkipDto ToDto(MarketItemGenerationSkip skip)
        => new(skip.PlayerId, skip.PlayerName, skip.Reason);

    private static bool TryValidate<T>(T model, out IResult? errorResult)
    {
        var context = new ValidationContext(model!);
        var results = new List<ValidationResult>();

        if (Validator.TryValidateObject(model!, context, results, validateAllProperties: true))
        {
            errorResult = null;
            return true;
        }

        var errors = results
            .SelectMany(result =>
                result.MemberNames.Any()
                    ? result.MemberNames
                    : new[] { "__all__" },
                (result, member) => new { Member = member, Message = result.ErrorMessage ?? "Valor inválido." })
            .GroupBy(item => item.Member)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Message).Distinct().ToArray());

        errorResult = Results.ValidationProblem(
            errors,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Falha na validação.");
        return false;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
