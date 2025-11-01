using System.Collections.Generic;
using System.Linq;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

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
        return marketApi;
    }

    private static async Task<IResult> HandleQueryAsync(
        HttpContext http,
        [AsParameters] MarketItemsRequest request,
        IMarketCycleService cycleService,
        IMarketItemsQueryService queryService,
        IWebHostEnvironment env,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("MarketItemsQuery");

        try
        {
            var cycle = await cycleService.ResolveAsync(request.CycleId, ct).ConfigureAwait(false);
            if (cycle is null)
            {
                return Results.NotFound(new { message = "Ciclo de mercado não encontrado." });
            }

            if (!request.TryBuildQuery(cycle.CycleId, out var query, out var errorResult))
            {
                return errorResult!;
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

    private sealed record MarketItemsRequest(
        [property: FromQuery(Name = "cycleId")] Guid? CycleId,
        [property: FromQuery(Name = "q")] string? Search,
        [property: FromQuery(Name = "pos")] int[]? Positions,
        [property: FromQuery(Name = "overallMin")] int? OverallMin,
        [property: FromQuery(Name = "overallMax")] int? OverallMax,
        [property: FromQuery(Name = "status")] string? Status,
        [property: FromQuery(Name = "sortBy")] string? SortBy,
        [property: FromQuery(Name = "sortOrder")] string? SortOrder,
        [property: FromQuery(Name = "page")] int Page = 1,
        [property: FromQuery(Name = "pageSize")] int PageSize = 20)
    {
        private const int MaxOverall = 150;

        public bool TryBuildQuery(Guid cycleId, out MarketItemsQuery query, out IResult? errorResult)
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

            var positionIds = Positions is { Length: > 0 }
                ? Positions
                    .Where(p => p is >= short.MinValue and <= short.MaxValue)
                    .Select(p => (short)p)
                    .Distinct()
                    .ToArray()
                : Array.Empty<short>();

            var statuses = ParseStatuses(Status);
            var (sortField, sortDescending) = ResolveSort(SortBy, SortOrder);

            var page = Page < 1 ? 1 : Page;
            var pageSize = PageSize < 1 ? 20 : PageSize;

            query = new MarketItemsQuery(
                cycleId,
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

        private static IReadOnlyList<MarketItemStatus> ParseStatuses(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return Array.Empty<MarketItemStatus>();
            }

            var tokens = raw
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Length == 0)
            {
                return Array.Empty<MarketItemStatus>();
            }

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
                {
                    result.Add(status);
                }
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
                {
                    descending = false;
                }
                else if (normalizedOrder is "desc" or "descending")
                {
                    descending = true;
                }
            }

            return (field, descending);
        }
    }
}
