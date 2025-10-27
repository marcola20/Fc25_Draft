using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Microsoft.AspNetCore.WebUtilities;

namespace Fc25Draft.Web.Services;

public class TeamsApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public TeamsApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<PagedResult<TeamListItemDto>> GetAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        var query = new Dictionary<string, string?>
        {
            ["page"] = Math.Max(1, page).ToString(),
            ["pageSize"] = Math.Max(1, pageSize).ToString()
        };

        if (!string.IsNullOrWhiteSpace(search))
        {
            query["q"] = search;
        }

        var url = QueryHelpers.AddQueryString("api/teams", query);
        var response = await client.GetAsync(url, ct);
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<PagedResult<TeamListItemDto>>(cancellationToken: ct);
        return result ?? new PagedResult<TeamListItemDto>(Array.Empty<TeamListItemDto>(), 0);
    }

    public async Task<TeamDetailsDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.GetAsync($"api/teams/{id}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TeamDetailsDto>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<TeamRosterDto>> GetRostersAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        var response = await client.GetAsync("api/teams/roster", ct);
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<TeamRosterDto>>(cancellationToken: ct);
        return result ?? Array.Empty<TeamRosterDto>();
    }

    public async Task<TeamRosterDto?> GetRosterByIdAsync(Guid id, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        var response = await client.GetAsync($"api/teams/{id}/roster", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<TeamRosterDto>(cancellationToken: ct);
    }

    public async Task CreateAsync(TeamCreateDto dto, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/teams", dto, ct);
        await EnsureSuccessAsync(response);
    }

    public async Task UpdateAsync(Guid id, TeamUpdateDto dto, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PutAsJsonAsync($"api/admin/teams/{id}", dto, ct);
        await EnsureSuccessAsync(response);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.DeleteAsync($"api/admin/teams/{id}", ct);
        await EnsureSuccessAsync(response);
    }

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
            // ignore
        }

        var message = error?.Message ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).";

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            message = "Ação permitida somente para administradores. Verifique o token informado.";
        }

        throw new InvalidOperationException(message);
    }

    private sealed record ApiErrorResponse(string? Message);
}
