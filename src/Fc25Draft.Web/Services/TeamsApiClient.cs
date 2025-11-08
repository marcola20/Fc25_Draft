using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
        return result ?? PagedResult<TeamListItemDto>.Empty(page, pageSize);
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

    public async Task<TeamIdentityDto?> GetIdentityByTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token inválido.", nameof(token));
        }

        var trimmed = token.Trim();
        var client = await _clientFactory.CreateAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "api/teams/me");
        request.Headers.TryAddWithoutValidation("X-Team-Token", trimmed);

        using var response = await client.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<TeamIdentityDto>(cancellationToken: ct);
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var message = await ReadErrorMessageAsync(response, ct) ?? "Token inválido.";
            throw new TeamApiException(message, response.StatusCode);
        }

        var error = await ReadErrorMessageAsync(response, ct) ?? $"Falha ao obter identidade do time. Código {(int)response.StatusCode}.";
        throw new TeamApiException(error, response.StatusCode);
    }

    public async Task<QuickSellResultDto> QuickSellAsync(Guid teamId, Guid playerId, string teamToken, CancellationToken ct = default)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("teamId inválido.", nameof(teamId));
        }

        if (playerId == Guid.Empty)
        {
            throw new ArgumentException("playerId inválido.", nameof(playerId));
        }

        if (string.IsNullOrWhiteSpace(teamToken))
        {
            throw new ArgumentException("Token inválido.", nameof(teamToken));
        }

        var client = await _clientFactory.CreateAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/teams/{teamId}/quick-sell/{playerId}");
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());

        using var response = await client.SendAsync(request, ct);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<QuickSellResultDto>(cancellationToken: ct);
            if (result is null)
            {
                throw new TeamApiException("Resposta inválida do servidor.", response.StatusCode);
            }

            return result;
        }

        var message = await ReadErrorMessageAsync(response, ct) ?? $"Falha ao realizar Quick Sell. Código {(int)response.StatusCode}.";
        throw new TeamApiException(message, response.StatusCode);
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

    public sealed class TeamApiException : Exception
    {
        public TeamApiException(string message, HttpStatusCode statusCode) : base(message)
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode { get; }
    }

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: ct);
            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return error!.Message;
            }
        }
        catch
        {
        }

        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }
        catch
        {
            return null;
        }
    }
}
