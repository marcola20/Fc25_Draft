using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Web.Extensions;
using Fc25Draft.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;

namespace Fc25Draft.Web.Endpoints.Market;

public static class MarketItemsEndpoints
{
    private static readonly Dictionary<string, MarketItemStatus> StatusLookup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["draft"] = MarketItemStatus.Draft,
        ["rascunho"] = MarketItemStatus.Draft,
        ["active"] = MarketItemStatus.Active,
        ["ativo"] = MarketItemStatus.Active,
        ["published"] = MarketItemStatus.Active,
        ["publicado"] = MarketItemStatus.Active,
        ["sold"] = MarketItemStatus.Sold,
        ["vendido"] = MarketItemStatus.Sold,
        ["settled"] = MarketItemStatus.Sold,
        ["finalizado"] = MarketItemStatus.Sold,
        ["canceled"] = MarketItemStatus.Canceled,
        ["cancelled"] = MarketItemStatus.Canceled,
        ["cancelado"] = MarketItemStatus.Canceled,
        ["expired"] = MarketItemStatus.Expired,
        ["expirado"] = MarketItemStatus.Expired
    };

    public static RouteGroupBuilder MapMarketItemsEndpoints(this RouteGroupBuilder marketApi)
    {
        marketApi.MapGet("/items", HandleQueryAsync).AllowAnonymous();
        marketApi.MapPost("/items/{itemId:guid}/bids", HandleBidAsync).AllowAnonymous();
        return marketApi;
    }

    private static async Task<IResult> HandleQueryAsync(
        HttpContext http,
        [AsParameters] MarketItemsRequest request,
        IMarketCycleService cycleService,
        IAuctionSettlementService settlementService,
        IMarketItemsQueryService queryService,
        IWebHostEnvironment env,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("MarketItemsQuery");

        try
        {
            IReadOnlyList<MarketCycleDto>? cycles;
            try
            {
                cycles = await ResolveTargetCyclesAsync(request, cycleService, ct).ConfigureAwait(false);
            }
            catch (MarketCycleUnavailableException ex)
            {
                var message = string.IsNullOrWhiteSpace(ex.Message)
                    ? "Este ciclo ainda não está ativo."
                    : ex.Message;
                return Results.Conflict(new { message });
            }

            if (cycles is null || cycles.Count == 0)
            {
                return Results.NotFound(new { message = "Ciclo de mercado não encontrado." });
            }

            if (!request.TryBuildQuery(cycles.Select(c => c.CycleId).ToArray(), out var query, out var errorResult))
                return errorResult!;

            if (SettlementThrottle.TryAcquire())
            {
                foreach (var cycleId in cycles.Select(c => c.CycleId).Distinct())
                {
                    await settlementService.SettleExpiredItemsAsync(cycleId, ct).ConfigureAwait(false);
                }
            }

            var result = await queryService.QueryAsync(query, ct).ConfigureAwait(false);

            http.Response.Headers["x-server-time-utc"] = DateTime.UtcNow.ToString("O");

            return Results.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Erro interno em GET /api/market/items: {Message}", ex.Message);

            var detail = env.IsDevelopment() ? ex.ToString() : ex.Message;
            return Results.Problem(
                title: "Failed to load market items.",
                detail: detail,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> HandleBidAsync(
        Guid itemId,
        PlaceBidRequest request,
        HttpContext http,
        IMarketService marketService,
        CancellationToken ct)
    {
        if (request is null)
            return Results.BadRequest(new { message = "Payload inválido." });

        var token = ResolveTeamToken(http, request.TeamToken);
        if (token is null)
            return Results.Json(new { message = "Token obrigatório." }, statusCode: StatusCodes.Status401Unauthorized);

        if (!EndpointHelpers.TryResolveRowVersion(http.Request, out var rowVersion, out var errorResult, allowFallbackToRowVersionHeader: true))
            return errorResult!;

        try
        {
            var updated = await marketService.PlaceBidAsync(itemId, token, request.Amount, rowVersion, ct).ConfigureAwait(false);

            EndpointHelpers.ApplyEtag(http.Response, updated.RowVersion);
            http.Response.Headers["X-RowVersion"] = updated.RowVersion.ToString(CultureInfo.InvariantCulture);

            return Results.Ok(updated);
        }
        catch (MarketForbiddenException ex)
        {
            return Results.Json(new { message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (MarketValidationException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (MarketConflictException ex)
        {
            return Results.Conflict(new { message = ex.Message });
        }
        catch (MarketPreconditionFailedException ex)
        {
            return EndpointHelpers.CreatePreconditionFailedProblem(ex.Message);
        }
        catch (MarketNotFoundException ex)
        {
            return Results.NotFound(new { message = ex.Message });
        }
    }

    private static string? ResolveTeamToken(HttpContext context, string? payloadToken)
    {
        var headerToken = context.Request.Headers["X-Team-Token"].FirstOrDefault();
        var token = !string.IsNullOrWhiteSpace(payloadToken) ? payloadToken : headerToken;
        return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
    }

    private static async Task<IReadOnlyList<MarketCycleDto>?> ResolveTargetCyclesAsync(
        MarketItemsRequest request,
        IMarketCycleService cycleService,
        CancellationToken ct)
    {
        if (request.CycleId.HasValue && request.CycleId.Value != Guid.Empty)
        {
            var cycle = await cycleService.ResolveAsync(request.CycleId, ct).ConfigureAwait(false);
            if (cycle is null)
            {
                return null;
            }

            if (cycle.Status == MarketCycleStatus.Draft)
            {
                throw new MarketCycleUnavailableException("Este ciclo ainda não está ativo.", HttpStatusCode.Conflict);
            }

            return new[] { cycle };
        }

        var activeCycles = await cycleService.ListActiveAsync(ct).ConfigureAwait(false);
        if (activeCycles.Count > 0)
        {
            return activeCycles;
        }

        var fallback = await cycleService.ResolveAsync(null, ct).ConfigureAwait(false);
        if (fallback is null)
        {
            return null;
        }

        if (fallback.Status == MarketCycleStatus.Draft)
        {
            throw new MarketCycleUnavailableException("Este ciclo ainda não está ativo.", HttpStatusCode.Conflict);
        }

        return new[] { fallback };
    }

    private sealed record MarketItemsRequest(
        [property: FromQuery(Name = "cycleId")] Guid? CycleId,
        [property: FromQuery(Name = "q")] string? Search,
        [property: FromQuery(Name = "pos")] int[]? Positions,
        [property: FromQuery(Name = "positions")] string? PositionsRaw,
        [property: FromQuery(Name = "overallMin")] int? OverallMin,
        [property: FromQuery(Name = "overallMax")] int? OverallMax,
        [property: FromQuery(Name = "status")] string? Status,
        [property: FromQuery(Name = "sortBy")] string? SortBy,
        [property: FromQuery(Name = "sortOrder")] string? SortOrder,
        [property: FromQuery(Name = "page")] int Page = 1,
        [property: FromQuery(Name = "pageSize")] int PageSize = 20)
    {
        private const int MaxOverall = 150;

        public bool TryBuildQuery(IReadOnlyList<Guid> cycleIds, out MarketItemsQuery query, out IResult? errorResult)
        {
            query = default!;
            errorResult = null;

            if (OverallMin is < 0 or > MaxOverall)
            {
                errorResult = Results.BadRequest(new { message = "Overall mínimo deve estar entre 0 e 150." });
                return false;
            }

            if (OverallMax is < 0 or > MaxOverall)
            {
                errorResult = Results.BadRequest(new { message = "Overall máximo deve estar entre 0 e 150." });
                return false;
            }

            if (OverallMin.HasValue && OverallMax.HasValue && OverallMin > OverallMax)
            {
                errorResult = Results.BadRequest(new { message = "Overall mínimo não pode ser maior que o overall máximo." });
                return false;
            }

            var search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();

            var positionIds = ParsePositionIds();

            var statuses = ParseStatuses(Status);
            var (sortField, sortDescending) = ResolveSort(SortBy, SortOrder);

            var page = Page < 1 ? 1 : Page;
            var pageSize = PageSize < 1 ? 20 : PageSize;

            var normalizedCycles = cycleIds?.Where(id => id != Guid.Empty).Distinct().ToArray() ?? Array.Empty<Guid>();

            if (normalizedCycles.Length == 0)
            {
                errorResult = Results.BadRequest(new { message = "Nenhum ciclo válido informado." });
                return false;
            }

            query = new MarketItemsQuery(
                normalizedCycles,
                search,
                positionIds,
                OverallMin,
                OverallMax,
                statuses,
                sortField,
                sortDescending,
                page,
                pageSize);

            return true;
        }

        private short[] ParsePositionIds()
        {
            var buffer = new HashSet<short>();

            if (Positions is { Length: > 0 })
                foreach (var value in Positions)
                    if (value >= short.MinValue && value <= short.MaxValue)
                        buffer.Add((short)value);

            if (!string.IsNullOrWhiteSpace(PositionsRaw))
            {
                var tokens = PositionsRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var token in tokens)
                {
                    if (string.IsNullOrWhiteSpace(token))
                        continue;

                    if (short.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) && numeric >= short.MinValue && numeric <= short.MaxValue)
                    {
                        buffer.Add(numeric);
                        continue;
                    }

                    if (PositionExtensions.TryParsePositionCode(token, out var codeId))
                    {
                        buffer.Add(codeId);
                        continue;
                    }

                    var nameId = PositionExtensions.ToPositionId(token);
                    if (nameId > 0)
                        buffer.Add((short)nameId);
                }
            }

            return buffer.Count == 0 ? Array.Empty<short>() : buffer.ToArray();
        }

    private static IReadOnlyList<MarketItemStatus> ParseStatuses(string? raw)
    {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<MarketItemStatus>();

            var tokens = raw
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Length == 0)
                return Array.Empty<MarketItemStatus>();

            var result = new HashSet<MarketItemStatus>();

            foreach (var token in tokens)
            {
                if (token.Equals("encerrado", StringComparison.OrdinalIgnoreCase) ||
                    token.Equals("closed", StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(MarketItemStatus.Sold);
                    result.Add(MarketItemStatus.Canceled);
                    result.Add(MarketItemStatus.Expired);
                    continue;
                }

                if (StatusLookup.TryGetValue(token, out var status))
                    result.Add(status);
            }

            return result.Count == 0 ? Array.Empty<MarketItemStatus>() : result.ToArray();
        }

        private static (MarketItemsSortField Field, bool Descending) ResolveSort(string? sortBy, string? sortOrder)
        {
            var descending = false;
            var token = sortBy?.Trim();

            if (!string.IsNullOrEmpty(token) && token.StartsWith("-", StringComparison.Ordinal))
            {
                descending = true;
                token = token[1..];
            }

            var normalizedField = token?.ToLowerInvariant();
            var field = normalizedField switch
            {
                "currentbid" or "current_bid" => MarketItemsSortField.CurrentBid,
                "expiresatutc" or "expires_at_utc" => MarketItemsSortField.ExpiresAtUtc,
                null or "" => MarketItemsSortField.ExpiresAtUtc,
                _ => MarketItemsSortField.ExpiresAtUtc
            };

            if (!string.IsNullOrWhiteSpace(sortOrder))
            {
                var normalizedOrder = sortOrder.Trim().ToLowerInvariant();
                if (normalizedOrder is "asc" or "ascending")
                    descending = false;
                else if (normalizedOrder is "desc" or "descending")
                    descending = true;
            }

            return (field, descending);
        }
    }

    private sealed record PlaceBidRequest(decimal Amount, string? TeamToken);
}
