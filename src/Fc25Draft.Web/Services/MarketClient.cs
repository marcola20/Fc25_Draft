using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Web.Models.Market;
using Microsoft.AspNetCore.WebUtilities;
using System.Text.Json.Serialization;

namespace Fc25Draft.Web.Services;

public class MarketClient
{
    private const string ServerTimeHeaderName = "x-server-time-utc";

    private readonly ApiClientFactory _clientFactory;

    public MarketClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
    }

    public DateTime? LastServerTimeUtc { get; private set; }

    public async Task<PagedResult<MarketItemVm>> GetItemsAsync(MarketQueryVm query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);

        var parameters = BuildQueryParameters(query);
        var url = QueryHelpers.AddQueryString("api/market/items", parameters);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<MarketItemVm>>(cancellationToken: ct)
            .ConfigureAwait(false);

        return result ?? new PagedResult<MarketItemVm>(Array.Empty<MarketItemVm>(), 0);
    }

    public async Task<MarketItemVm> PlaceBidAsync(BidRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ItemId == Guid.Empty)
        {
            throw new ArgumentException("O item é obrigatório.", nameof(request));
        }

        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        var url = $"api/market/items/{request.ItemId}/bid";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new MarketBidApiRequest(request.Amount, request.TeamToken))
        };

        ApplyRowVersion(httpRequest, request.RowVersion);

        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<MarketItemVm>(cancellationToken: ct)
            .ConfigureAwait(false);

        return result ?? throw new InvalidOperationException("Resposta inesperada do servidor ao registrar o lance.");
    }

    public async Task<MarketItemVm> BuyNowAsync(BuyNowRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ItemId == Guid.Empty)
        {
            throw new ArgumentException("O item é obrigatório.", nameof(request));
        }

        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        var url = $"api/market/items/{request.ItemId}/buy-now";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new MarketBuyNowApiRequest(request.TeamToken))
        };

        ApplyRowVersion(httpRequest, request.RowVersion);

        using var response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var result = await response.Content.ReadFromJsonAsync<MarketItemVm>(cancellationToken: ct)
            .ConfigureAwait(false);

        return result ?? throw new InvalidOperationException("Resposta inesperada do servidor ao concluir a compra imediata.");
    }

    private static IEnumerable<KeyValuePair<string, string?>> BuildQueryParameters(MarketQueryVm query)
    {
        var parameters = new List<KeyValuePair<string, string?>>
        {
            new("page", Math.Max(1, query.Page).ToString(CultureInfo.InvariantCulture)),
            new("pageSize", Math.Max(1, query.PageSize).ToString(CultureInfo.InvariantCulture))
        };

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            parameters.Add(new KeyValuePair<string, string?>("q", query.SearchTerm));
        }

        if (query.PositionIds is { Count: > 0 })
        {
            foreach (var positionId in query.PositionIds.Where(p => p > 0).Distinct())
            {
                parameters.Add(new KeyValuePair<string, string?>("pos", positionId.ToString(CultureInfo.InvariantCulture)));
            }
        }

        if (query.OverallMin.HasValue)
        {
            parameters.Add(new KeyValuePair<string, string?>("overallMin", query.OverallMin.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (query.OverallMax.HasValue)
        {
            parameters.Add(new KeyValuePair<string, string?>("overallMax", query.OverallMax.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            parameters.Add(new KeyValuePair<string, string?>("status", query.Status));
        }

        if (!string.IsNullOrWhiteSpace(query.SortBy))
        {
            parameters.Add(new KeyValuePair<string, string?>("sortBy", query.SortBy));
        }

        if (!string.IsNullOrWhiteSpace(query.SortOrder))
        {
            parameters.Add(new KeyValuePair<string, string?>("sortOrder", query.SortOrder));
        }

        return parameters;
    }

    private void ApplyRowVersion(HttpRequestMessage request, uint rowVersion)
    {
        if (rowVersion == 0)
        {
            return;
        }

        var value = rowVersion.ToString(CultureInfo.InvariantCulture);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{value}\"");
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        UpdateServerTime(response);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = await ReadErrorMessageAsync(response, ct).ConfigureAwait(false);

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new MarketValidationException(message);
            case HttpStatusCode.Conflict:
                throw new MarketConflictException(message);
            case HttpStatusCode.PreconditionFailed:
                throw new MarketPreconditionFailedException(message);
            case HttpStatusCode.Forbidden:
                throw new MarketForbiddenException(message);
            case HttpStatusCode.NotFound:
                throw new MarketNotFoundException(message);
            case HttpStatusCode.Unauthorized:
                throw new InvalidOperationException(message ?? "Operação não autorizada.");
            default:
                throw new InvalidOperationException(message ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).");
        }
    }

    private void UpdateServerTime(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(ServerTimeHeaderName, out var values))
        {
            return;
        }

        var headerValue = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return;
        }

        if (DateTimeOffset.TryParse(headerValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            LastServerTimeUtc = parsed.UtcDateTime;
        }
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ApiMessageResponse>(cancellationToken: ct)
                .ConfigureAwait(false);
            return payload?.Message;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private sealed record MarketBidApiRequest(decimal Amount, string? TeamToken);

    private sealed record MarketBuyNowApiRequest(string? TeamToken);

    private sealed record ApiMessageResponse([property: JsonPropertyName("message")] string? Message);
}
