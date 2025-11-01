using System.Net.Http.Json;
using System.Collections.Generic;
using Fc25Draft.Core.DTOs;
using Microsoft.AspNetCore.WebUtilities;

namespace Fc25Draft.Web.Services;

public class TransfersClient
{
    private readonly ApiClientFactory _clientFactory;

    public TransfersClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<PagedResult<TransferListItemDto>> QueryAsync(TransferHistoryQueryRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await _clientFactory.CreateAsync(includeAdminToken: true).ConfigureAwait(false);

        var query = BuildQuery(request);
        var url = QueryHelpers.AddQueryString("api/transfers/history", query);

        using var response = await client.GetAsync(url, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<TransferListItemDto>>(cancellationToken: ct)
            .ConfigureAwait(false);

        return result ?? PagedResult<TransferListItemDto>.Empty(request.Page, request.PageSize);
    }

    public async Task<TransferListItemDto?> GetByIdAsync(Guid transferId, CancellationToken ct)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true).ConfigureAwait(false);
        using var response = await client.GetAsync($"api/transfers/{transferId}", ct).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<TransferListItemDto>(cancellationToken: ct).ConfigureAwait(false);
    }

    private static IEnumerable<KeyValuePair<string, string?>> BuildQuery(TransferHistoryQueryRequest request)
    {
        var list = new List<KeyValuePair<string, string?>>
        {
            new("page", Math.Max(1, request.Page).ToString()),
            new("pageSize", Math.Max(1, request.PageSize).ToString())
        };

        if (request.TeamId.HasValue)
        {
            list.Add(new KeyValuePair<string, string?>("teamId", request.TeamId.Value.ToString()));
        }

        if (request.PlayerId.HasValue)
        {
            list.Add(new KeyValuePair<string, string?>("playerId", request.PlayerId.Value.ToString()));
        }

        if (request.DateFromUtc.HasValue)
        {
            list.Add(new KeyValuePair<string, string?>("from", request.DateFromUtc.Value.ToString("O")));
        }

        if (request.DateToUtc.HasValue)
        {
            list.Add(new KeyValuePair<string, string?>("to", request.DateToUtc.Value.ToString("O")));
        }

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            list.Add(new KeyValuePair<string, string?>("q", request.Query));
        }

        return list;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>().ConfigureAwait(false);
        var message = error?.Message ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).";

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            message = "Acesso restrito a administradores.";
        }

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private sealed record ApiErrorResponse(string? Message);
}

public sealed class TransferHistoryQueryRequest
{
    public Guid? TeamId { get; set; }
    public int? PlayerId { get; set; }
    public DateTime? DateFromUtc { get; set; }
    public DateTime? DateToUtc { get; set; }
    public string? Query { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
