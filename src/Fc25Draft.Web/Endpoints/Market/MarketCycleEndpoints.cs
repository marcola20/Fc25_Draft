using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Web.Models.MarketCycles;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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
        catch (DbUpdateException eax) 
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
