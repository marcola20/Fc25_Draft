using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using System.Globalization;

namespace Fc25Draft.Web.Extensions
{
    public static class EndpointHelpers
    {
        public static bool TryGetAdminToken(HttpContext context, out string? adminToken, out IResult? errorResult)
        {
            adminToken = null;
            errorResult = null;

            if (!context.Request.Headers.TryGetValue("Authorization", out var authorizationHeader))
            {
                errorResult = Results.Json(new { message = "Token de administrador ausente." }, statusCode: StatusCodes.Status403Forbidden);
                return false;
            }

            var headerValue = authorizationHeader.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                errorResult = Results.Json(new { message = "Token de administrador ausente." }, statusCode: StatusCodes.Status403Forbidden);
                return false;
            }

            const string bearerPrefix = "Bearer ";
            if (!headerValue.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errorResult = Results.Json(new { message = "Token de administrador inválido." }, statusCode: StatusCodes.Status403Forbidden);
                return false;
            }

            var tokenValue = headerValue[bearerPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(tokenValue))
            {
                errorResult = Results.Json(new { message = "Token de administrador inválido." }, statusCode: StatusCodes.Status403Forbidden);
                return false;
            }

            adminToken = tokenValue;
            return true;
        }

        public static void ApplyEtag(HttpResponse response, uint rowVersion)
            => response.Headers[HeaderNames.ETag] = $"W/\"{rowVersion}\"";

        public static bool TryResolveRowVersion(HttpRequest request, out uint rowVersion, out IResult? errorResult, bool allowFallbackToRowVersionHeader = false)
        {
            rowVersion = 0;
            errorResult = null;

            var requirementDetail = allowFallbackToRowVersionHeader
                ? "Os cabeçalhos If-Match ou X-RowVersion são obrigatórios para esta operação."
                : "O cabeçalho If-Match é obrigatório para esta operação.";

            var invalidDetail = allowFallbackToRowVersionHeader
                ? "Não foi possível interpretar o valor informado nos cabeçalhos If-Match ou X-RowVersion."
                : "Não foi possível interpretar o valor do cabeçalho If-Match.";

            string? rawValue = null;
            var fromIfMatch = false;

            if (request.Headers.TryGetValue(HeaderNames.IfMatch, out var headerValues))
            {
                rawValue = headerValues.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                fromIfMatch = !string.IsNullOrWhiteSpace(rawValue);
            }

            if (string.IsNullOrWhiteSpace(rawValue) && allowFallbackToRowVersionHeader)
            {
                if (request.Headers.TryGetValue("X-RowVersion", out var customValues))
                    rawValue = customValues.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            }

            if (string.IsNullOrWhiteSpace(rawValue))
            {
                errorResult = Results.Problem(
                    title: "Pré-condição obrigatória.",
                    detail: requirementDetail,
                    statusCode: StatusCodes.Status428PreconditionRequired,
                    type: "https://httpstatuses.com/428"
                );
                return false;
            }

            var parsed = rawValue.Trim();

            if (fromIfMatch)
            {
                if (parsed.StartsWith("W/\"", StringComparison.OrdinalIgnoreCase) && parsed.EndsWith("\""))
                    parsed = parsed[3..^1];
                else if (parsed.StartsWith('"') && parsed.EndsWith('"'))
                    parsed = parsed[1..^1];
            }

            if (!uint.TryParse(parsed, out rowVersion))
            {
                errorResult = Results.Problem(
                    title: "Cabeçalho de versão inválido.",
                    detail: invalidDetail,
                    statusCode: StatusCodes.Status400BadRequest,
                    type: "https://httpstatuses.com/400"
                );
                return false;
            }

            return true;
        }

        public static IResult CreateValidationProblem(string message, object? errors = null)
        {
            var problem = new ProblemDetails
            {
                Title = "Falha na validação.",
                Detail = message,
                Status = StatusCodes.Status422UnprocessableEntity,
                Type = "https://httpstatuses.com/422"
            };
            if (errors is not null) problem.Extensions["errors"] = errors;
            return Results.Problem(problem);
        }

        public static IResult CreateConflictProblem(string message) =>
            Results.Problem(new ProblemDetails
            {
                Title = "Conflito ao processar a requisição.",
                Detail = message,
                Status = StatusCodes.Status409Conflict,
                Type = "https://httpstatuses.com/409"
            });

        public static IResult CreateNotFoundProblem(string message) =>
            Results.Problem(new ProblemDetails
            {
                Title = "Recurso não encontrado.",
                Detail = message,
                Status = StatusCodes.Status404NotFound,
                Type = "https://httpstatuses.com/404"
            });

        public static IResult CreatePreconditionFailedProblem(string message) =>
            Results.Problem(new ProblemDetails
            {
                Title = "Pré-condição não satisfeita.",
                Detail = message,
                Status = StatusCodes.Status412PreconditionFailed,
                Type = "https://httpstatuses.com/412"
            });

        public static TransferHistoryItemDto MapTransferHistoryToDto(TransferHistory history)
        {
            if (history is null)
            {
                throw new ArgumentNullException(nameof(history));
            }

            var playerName = history.Player?.Name ?? string.Empty;
            var fromTeamName = history.FromTeam?.TeamName ?? "Mercado Livre";
            var toTeamName = history.ToTeam?.TeamName;

            return new TransferHistoryItemDto(
                history.TransferId,
                history.PerformedAtUtc,
                history.PlayerId,
                playerName,
                history.FromTeamId,
                fromTeamName,
                history.ToTeamId,
                toTeamName,
                history.Amount,
                (int)history.Type,
                history.Type.ToDisplayName(),
                history.Notes,
                history.PerformedBy);
        }

        public static MarketItemVm MapToMarketItemVm(MarketItemDto dto) => new MarketItemVm
        {
            ItemId = dto.ItemId,
            CycleId = dto.CycleId,
            PlayerId = dto.PlayerId,
            PlayerName = dto.PlayerName,
            PositionId = dto.Position.ToPositionId(),
            Overall = dto.Ovr,
            BasePrice = dto.BasePrice,
            CurrentLeaderTeamId = dto.CurrentLeaderTeamId,
            CurrentLeaderTeamName = dto.CurrentLeaderTeamName,
            CurrentLeaderAmount = dto.CurrentLeaderAmount,
            BuyNowPrice = dto.BuyNowPrice,
            MinIncrement = dto.MinIncrement,
            RequiredMinBid = dto.RequiredMinBid,
            ExpiresAtUtc = dto.ExpiresAtUtc,
            Status = dto.Status.ToString(),
            RowVersion = dto.RowVersion.ToString(CultureInfo.InvariantCulture)
        };
    }
}
