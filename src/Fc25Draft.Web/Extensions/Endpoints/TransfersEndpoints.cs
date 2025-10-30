using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Fc25Draft.Web.Extensions.Endpoints
{
    public static class TransfersEndpoints
    {
        public static IEndpointRouteBuilder MapTransfersEndpoints(this IEndpointRouteBuilder api)
        {
            var transfersApi = api.MapGroup("/transfers");

            transfersApi.MapGet("/history", async (
                [FromQuery] Guid? teamId,
                [FromQuery] Guid? playerId,
                [FromQuery] string? type,
                [FromQuery] DateTime? from,
                [FromQuery] DateTime? to,
                [FromQuery] int? page,
                [FromQuery] int? pageSize,
                ITransfersQueryService transfersQueryService,
                CancellationToken ct) =>
            {
                TransferType? typeFilter = null;

                if (!string.IsNullOrWhiteSpace(type))
                {
                    var normalizedType = type.Trim();

                    if (Enum.TryParse<TransferType>(normalizedType, ignoreCase: true, out var parsedType))
                    {
                        typeFilter = parsedType;
                    }
                    else if (int.TryParse(normalizedType, out var numericType)
                             && Enum.IsDefined(typeof(TransferType), numericType))
                    {
                        typeFilter = (TransferType)numericType;
                    }
                    else
                    {
                        return Results.BadRequest(new { message = "Tipo de transferência inválido." });
                    }
                }

                var filter = new TransfersFilter
                {
                    TeamId = teamId,
                    PlayerId = playerId,
                    Type = typeFilter,
                    FromUtc = from,
                    ToUtc = to,
                    Page = page ?? 1,
                    PageSize = pageSize ?? 20
                };

                try
                {
                    var result = await transfersQueryService.QueryHistoryAsync(filter, ct);
                    return Results.Ok(result);
                }
                catch (ArgumentException ex)
                {
                    return Results.BadRequest(new { message = ex.Message });
                }
            }).AllowAnonymous();

            return api;
        }
    }
}
