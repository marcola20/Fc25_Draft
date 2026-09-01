using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Web.Models.Transfers;
using Microsoft.AspNetCore.WebUtilities;

namespace Fc25Draft.Web.Services;

public class TransfersClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly ApiClientFactory _clientFactory;

    public TransfersClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<PagedResult<TransferListItemDto>> GetHistoryAsync(TransferHistoryQueryRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = new Dictionary<string, string?>
        {
            ["page"] = Math.Max(1, request.Page).ToString(),
            ["pageSize"] = Math.Clamp(request.PageSize, 1, 100).ToString()
        };

        if (request.TeamId.HasValue)
        {
            query["teamId"] = request.TeamId.Value.ToString("D");
        }

        if (request.PlayerId.HasValue)
        {
            query["playerId"] = request.PlayerId.Value.ToString("D");
        }

        if (request.CycleId.HasValue)
        {
            query["cycleId"] = request.CycleId.Value.ToString("D");
        }

        if (request.DateFromUtc.HasValue)
        {
            query["dateFromUtc"] = request.DateFromUtc.Value.ToString("O");
        }

        if (request.DateToUtc.HasValue)
        {
            query["dateToUtc"] = request.DateToUtc.Value.ToString("O");
        }

        if (!string.IsNullOrWhiteSpace(request.NotesQuery))
        {
            query["q"] = request.NotesQuery;
        }

        if (request.OnlyAcquisitions)
        {
            query["type"] = "1"; // TransferType.MarketAuction
        }

        query["sortBy"] = request.SortBy;
        query["sortDir"] = request.SortDescending ? "desc" : "asc";

        var url = QueryHelpers.AddQueryString("/api/transfers/history", query);
        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);

        using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException($"GET {url} -> {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}", null, response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<PagedResult<TransferListItemDto>>(SerializerOptions, ct).ConfigureAwait(false);
        return payload ?? PagedResult<TransferListItemDto>.Empty(request.Page, request.PageSize);
    }

    public async Task<TransferDetailsDto?> GetByIdAsync(Guid transferId, CancellationToken ct)
    {
        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        using var response = await client.GetAsync($"/api/transfers/{transferId:D}", ct).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException($"GET /api/transfers/{transferId:D} -> {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}", null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<TransferDetailsDto>(SerializerOptions, ct).ConfigureAwait(false);
    }
}
