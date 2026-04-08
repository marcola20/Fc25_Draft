using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.WebUtilities;

namespace Fc25Draft.Web.Services;

public class PlayersApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public PlayersApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<PagedResult<PlayerListItemDto>> GetAsync(string? search,
                                                               IReadOnlyCollection<short>? positionIds,
                                                               bool onlyAvailable,
                                                               int? overallMin,
                                                               int? overallMax,
                                                               string? sortBy,
                                                               string? sortOrder,
                                                               int page,
                                                               int pageSize,
                                                               CancellationToken ct = default)
    { 
        var client = await _clientFactory.CreateAsync();

        var query = new List<KeyValuePair<string, string?>>
        {
            new("page", Math.Max(1, page).ToString()),
            new("pageSize", Math.Max(1, pageSize).ToString())
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query.Add(new KeyValuePair<string, string?>("q", search));
        }

        if (positionIds is { Count: > 0 })
        {
            foreach (var id in positionIds.Distinct())
            {
                query.Add(new KeyValuePair<string, string?>("pos", id.ToString()));
            }
        }

        if (onlyAvailable)
        {
            query.Add(new KeyValuePair<string, string?>("onlyAvailable", "true"));
        }

        if (overallMin.HasValue)
        {
            query.Add(new KeyValuePair<string, string?>("overallMin", overallMin.Value.ToString()));
        }

        if (overallMax.HasValue)
        {
            query.Add(new KeyValuePair<string, string?>("overallMax", overallMax.Value.ToString()));
        }

        if (!string.IsNullOrWhiteSpace(sortBy))
        {
            query.Add(new KeyValuePair<string, string?>("sortBy", sortBy));
        }

        if (!string.IsNullOrWhiteSpace(sortOrder))
        {
            query.Add(new KeyValuePair<string, string?>("sortOrder", sortOrder));
        }

        var url = QueryHelpers.AddQueryString("api/players", query);
        var response = await client.GetAsync(url, ct);
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<PlayerListItemDto>>(cancellationToken: ct);
        return result ?? PagedResult<PlayerListItemDto>.Empty(page, pageSize);
    }

    public async Task<PlayerDetailsDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        var response = await client.GetAsync($"api/players/{id}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<PlayerDetailsDto>(cancellationToken: ct);
    }

    public async Task CreateAsync(PlayerCreateDto dto, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/players", dto, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"API {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }
        await EnsureSuccessAsync(response);
    }

    public async Task UpdateAsync(int id, PlayerUpdateDto dto, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PutAsJsonAsync($"api/admin/players/{id}", dto, ct);
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.DeleteAsync($"api/admin/players/{id}", ct);
        await EnsureSuccessAsync(response);
    }

    public async Task<PlayerImportResultDto> ImportAsync(IBrowserFile file, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);

        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(file.OpenReadStream(MaxImportFileSize, ct));
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "text/csv" : file.ContentType;
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", file.Name);

        var response = await client.PostAsync("api/admin/players/import", content, ct);
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<PlayerImportResultDto>(cancellationToken: ct);
        return result ?? new PlayerImportResultDto(0, Array.Empty<string>());
    }

    private const long MaxImportFileSize = 5 * 1024 * 1024;

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        }
        catch
        {
            // ignore parsing errors
        }

        string message = error?.Message ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).";

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            message = "Ação permitida somente para administradores. Verifique o token informado.";
        }

        throw new InvalidOperationException(message);
    }

    private sealed record ApiErrorResponse(string? Message);
}
