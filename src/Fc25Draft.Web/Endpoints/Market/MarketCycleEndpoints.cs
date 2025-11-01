using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Web.Models.MarketCycles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Fc25Draft.Web.Endpoints.Market;

public static class MarketCycleEndpoints
{
    public static RouteGroupBuilder MapMarketCycleEndpoints(this RouteGroupBuilder marketApi)
    {
        var cycles = marketApi.MapGroup("/cycles").RequireAuthorization("AdminOnly"); 
        cycles.MapPost("", HandleCreateAsync);
        cycles.MapGet("", HandleQueryAsync);
        cycles.MapGet("/{cycleId:guid}", HandleGetAsync);
        cycles.MapPatch("/{cycleId:guid}/status", HandleStatusUpdateAsync);
        cycles.MapPost("/{cycleId:guid}/items:preview", HandlePreviewAsync);
        cycles.MapPost("/{cycleId:guid}/items:generate", HandleGenerateAsync);
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
        Guid cycleId,
        MarketCycleStatusUpdateRequest request,
        IMarketCycleAdminService service,
        IAuctionSettlementService settlementService,
        CancellationToken ct)
    {
        if (!TryValidate(request, out var validationError))
        {
            return validationError!;
        }

        var status = request.Status ?? MarketCycleStatus.Draft;

        try
        {
            var updated = await service.UpdateStatusAsync(cycleId, status, request.ForceClose, ct).ConfigureAwait(false);
            if (updated.Status == MarketCycleStatus.Closed)
            {
                var summary = await settlementService.SettleAllOpenItemsOnCycleCloseAsync(cycleId, ct).ConfigureAwait(false);
                return Results.Ok(new { ciclo = updated, vendidos = summary.Sold, expirados = summary.Expired });
            }

            return Results.Ok(updated);
        }
        catch (MarketNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
        catch (MarketConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (MarketValidationException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
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
        var filters = request.Filters ?? new MarketItemGenerationFiltersDto();
        var lifecycle = request.Lifecycle ?? new MarketItemGenerationLifecycleDto();

        var playerIds = filters.PlayerIds?.Where(id => id > 0).Distinct().ToArray();
        var positionIds = filters.PositionIds?.Distinct().ToArray();

        return new MarketItemGenerationOptions(
            request.DesiredCount,
            request.Seed,
            new MarketItemGenerationFilters(
                playerIds?.Length > 0 ? playerIds : null,
                positionIds?.Length > 0 ? positionIds : null,
                filters.MinOverall,
                filters.MaxOverall,
                filters.MinAge,
                filters.MaxAge,
                filters.OnlyFreeAgents),
            new MarketItemLifecycleOptions(
                lifecycle.PublishAtUtc,
                lifecycle.ExpiresAtUtc,
                lifecycle.DurationHours));
    }

    private static MarketItemGenerationPreviewDto ToDto(MarketItemGenerationPreview preview)
    {
        return new MarketItemGenerationPreviewDto(
            preview.RequestedCount,
            preview.EligibleCount,
            preview.Seed,
            preview.Items.Select(ToDto).ToList());
    }

    private static MarketItemGenerationResultDto ToDto(MarketItemGenerationResult result)
    {
        return new MarketItemGenerationResultDto(
            result.RequestedCount,
            result.EligibleCount,
            result.Seed,
            result.CreatedCount,
            result.SkippedExistingCount,
            result.Items.Select(ToDto).ToList());
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
            item.BasePrice,
            item.BuyNowPrice,
            item.MinIncrement,
            item.ExpiresAtUtc);
    }

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
