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

    public async Task<TeamIdentityDto?> ResolveIdentityAsync(string teamToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(teamToken))
        {
            throw new ArgumentException("Token do time é obrigatório.", nameof(teamToken));
        }

        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/teams/me");
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());

        using var response = await client.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var message = await ReadErrorMessageAsync(response, ct) ?? "Token do time inválido.";
            throw new InvalidOperationException(message);
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct)
                ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).";
            throw new InvalidOperationException(message);
        }

        return await response.Content.ReadFromJsonAsync<TeamIdentityDto>(cancellationToken: ct);
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

    public async Task<QuickSellResultDto> QuickSellAsync(Guid teamId, Guid playerId, string teamToken, CancellationToken ct = default)
    {
        if (teamId == Guid.Empty)
            throw new ArgumentException("Time inválido.", nameof(teamId));

        if (playerId == Guid.Empty)
            throw new ArgumentException("Jogador inválido.", nameof(playerId));

        if (string.IsNullOrWhiteSpace(teamToken))
            throw new InvalidOperationException("Token do time é obrigatório.");

        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/teams/{teamId}/quick-sell/{playerId}");
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());

        try
        {
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var message = await ReadErrorMessageAsync(response, ct) ?? $"Falha ao processar a venda rápida ({response.StatusCode}).";

                throw response.StatusCode switch
                {
                    HttpStatusCode.NotFound => new KeyNotFoundException(message),
                    HttpStatusCode.Conflict => new InvalidOperationException(message),
                    HttpStatusCode.Unauthorized => new InvalidOperationException(message),
                    HttpStatusCode.Forbidden => new InvalidOperationException(message),
                    _ => new InvalidOperationException(message)
                };
            }

            var payload = await response.Content.ReadFromJsonAsync<QuickSellResultDto>(cancellationToken: ct);
            return payload ?? throw new InvalidOperationException("Resposta inválida do servidor.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Erro ao enviar requisição para o servidor.", ex);
        }
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

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: ct);
            return error?.Message;
        }
        catch
        {
            return null;
        }
    }

    private sealed record ApiErrorResponse(string? Message);
}
