using System.Globalization;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Web.Endpoints.Transfers;
using Microsoft.AspNetCore.Mvc;

namespace Fc25Draft.Web.Extensions.Endpoints
{
    public static class TransfersEndpoints
    {
        public static IEndpointRouteBuilder MapTransfersEndpoints(this IEndpointRouteBuilder api)
        {
            var transfersApi = api.MapGroup("/transfers");

            transfersApi.MapGet("/history", HandleHistoryAsync).AllowAnonymous();
            transfersApi.MapGet("/{transferId:guid}", HandleGetByIdAsync).AllowAnonymous();
            transfersApi.MapTransferOffersEndpoints();

            return api;
        }

        private static async Task<IResult> HandleHistoryAsync(
            [AsParameters] TransfersHistoryQueryParameters request,
            ITransfersQueryService transfersQueryService,
            CancellationToken ct)
        {
            if (!request.TryCreateFilter(out var filter, out var errorResult))
            {
                return errorResult!;
            }

            try
            {
                var result = await transfersQueryService.QueryHistoryAsync(filter, ct).ConfigureAwait(false);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { message = ex.Message });
            }
        }

        private static async Task<IResult> HandleGetByIdAsync(
            Guid transferId,
            ITransfersQueryService transfersQueryService,
            CancellationToken ct)
        {
            if (transferId == Guid.Empty)
            {
                return Results.BadRequest(new { message = "Identificador de transferência inválido." });
            }

            var transfer = await transfersQueryService.GetByIdAsync(transferId, ct).ConfigureAwait(false);
            return transfer is null
                ? Results.NotFound(new { message = "Transferência não encontrada." })
                : Results.Ok(transfer);
        }

        private sealed record TransfersHistoryQueryParameters
        {
            [FromQuery(Name = "teamId")] public string? TeamIdRaw { get; init; }
            [FromQuery(Name = "playerId")] public string? PlayerIdRaw { get; init; }
            [FromQuery(Name = "type")] public string? TypeRaw { get; init; }
            [FromQuery(Name = "dateFromUtc")] public string? FromUtcRaw { get; init; }
            [FromQuery(Name = "dateToUtc")] public string? ToUtcRaw { get; init; }
            [FromQuery(Name = "q")] public string? NotesQuery { get; init; }
            [FromQuery(Name = "page")] public string? PageRaw { get; init; }
            [FromQuery(Name = "pageSize")] public string? PageSizeRaw { get; init; }

            public bool TryCreateFilter(out TransfersFilter filter, out IResult? errorResult)
            {
                filter = default!;
                errorResult = null;

                if (!TryParseGuid(TeamIdRaw, "teamId", out var teamId, out errorResult) ||
                    !TryParseGuid(PlayerIdRaw, "playerId", out var playerId, out errorResult))
                {
                    return false;
                }

                if (!TryParseDateTime(FromUtcRaw, "from", out var fromUtc, out errorResult) ||
                    !TryParseDateTime(ToUtcRaw, "to", out var toUtc, out errorResult))
                {
                    return false;
                }

                if (!TryParseTransferType(TypeRaw, out var type, out errorResult))
                {
                    return false;
                }

                if (!TryParseInt(PageRaw, "page", out var page, out errorResult) ||
                    !TryParseInt(PageSizeRaw, "pageSize", out var pageSize, out errorResult))
                {
                    return false;
                }

                if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
                {
                    errorResult = Results.BadRequest(new { message = "A data inicial deve ser menor ou igual à data final." });
                    return false;
                }

                var sanitizedPage = page.HasValue && page.Value > 0 ? page.Value : 1;
                var sanitizedPageSize = pageSize.HasValue && pageSize.Value > 0 ? Math.Min(pageSize.Value, 100) : 20;

                filter = new TransfersFilter
                {
                    TeamId = NormalizeGuid(teamId),
                    PlayerId = NormalizeGuid(playerId),
                    Type = type,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    NotesQuery = string.IsNullOrWhiteSpace(NotesQuery) ? null : NotesQuery.Trim(),
                    Page = sanitizedPage,
                    PageSize = sanitizedPageSize
                };

                return true;
            }

            private static bool TryParseGuid(string? value, string parameterName, out Guid? result, out IResult? errorResult)
            {
                result = null;
                errorResult = null;

                if (string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }

                if (!Guid.TryParse(value.Trim(), out var parsed))
                {
                    errorResult = Results.BadRequest(new { message = $"Parâmetro \"{parameterName}\" deve ser um GUID válido." });
                    return false;
                }

                result = parsed;
                return true;
            }

            private static bool TryParseDateTime(string? value, string parameterName, out DateTime? result, out IResult? errorResult)
            {
                result = null;
                errorResult = null;

                if (string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }

                if (!DateTimeOffset.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                {
                    errorResult = Results.BadRequest(new { message = $"Parâmetro \"{parameterName}\" possui formato de data/hora inválido. Utilize o padrão ISO 8601 (ex.: 2024-01-31T12:00:00Z)." });
                    return false;
                }

                result = parsed.UtcDateTime;
                return true;
            }

            private static bool TryParseTransferType(string? value, out TransferType? type, out IResult? errorResult)
            {
                type = null;
                errorResult = null;

                if (string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }

                var normalized = value.Trim();

                if (Enum.TryParse<TransferType>(normalized, ignoreCase: true, out var parsedEnum))
                {
                    type = parsedEnum;
                    return true;
                }

                if (int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericValue) &&
                    Enum.IsDefined(typeof(TransferType), numericValue))
                {
                    type = (TransferType)numericValue;
                    return true;
                }

                errorResult = Results.BadRequest(new { message = "Tipo de transferência inválido." });
                return false;
            }

            private static bool TryParseInt(string? value, string parameterName, out int? result, out IResult? errorResult)
            {
                result = null;
                errorResult = null;

                if (string.IsNullOrWhiteSpace(value))
                {
                    return true;
                }

                if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                {
                    errorResult = Results.BadRequest(new { message = $"Parâmetro \"{parameterName}\" deve ser um número inteiro válido." });
                    return false;
                }

                result = parsed;
                return true;
            }

            private static Guid? NormalizeGuid(Guid? value)
                => value.HasValue && value.Value == Guid.Empty ? null : value;
        }
    }
}
