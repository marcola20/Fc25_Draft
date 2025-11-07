using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Fc25Draft.Core.DTOs;
using Microsoft.AspNetCore.WebUtilities;

namespace Fc25Draft.Web.Services;

public class TeamsApiClient
{
    private readonly ApiClientFactory _clientFactory;
    private readonly TeamAccessService _teamAccess;

    public TeamsApiClient(ApiClientFactory clientFactory, TeamAccessService teamAccess)
    {
        _clientFactory = clientFactory;
        _teamAccess = teamAccess;
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

    public async Task<QuickSellResultDto> QuickSellAsync(Guid teamId, int playerId, CancellationToken ct = default)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (playerId <= 0)
        {
            throw new ArgumentException("Jogador inválido.", nameof(playerId));
        }

        var token = await _teamAccess.GetTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new TeamTokenMissingException();
        }

        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/teams/{teamId:D}/quick-sell/{playerId}");
        request.Headers.TryAddWithoutValidation("X-Team-Token", token);
        request.Content = JsonContent.Create(new { TeamToken = token });

        using var response = await client.SendAsync(request, ct);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            var message = await ReadErrorMessageAsync(response)
                ?? "Token do time inválido ou expirado. Informe novamente.";
            throw new QuickSellException(message, response.StatusCode);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var message = await ReadErrorMessageAsync(response)
                ?? "Time ou jogador não encontrado.";
            throw new QuickSellException(message, response.StatusCode);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var message = await ReadErrorMessageAsync(response)
                ?? "Não foi possível concluir o quick sell. Verifique as condições do elenco.";
            throw new QuickSellException(message, response.StatusCode);
        }

        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<QuickSellResultDto>(cancellationToken: ct);
        if (result is null)
        {
            throw new InvalidOperationException("Resposta inválida ao executar o quick sell.");
        }

        return result;
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

    private static async Task<string?> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        if (response.Content is null)
        {
            return null;
        }

        try
        {
            var payload = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("message", out var messageProp) && messageProp.ValueKind == JsonValueKind.String)
                {
                    return messageProp.GetString();
                }

                if (root.TryGetProperty("detail", out var detailProp) && detailProp.ValueKind == JsonValueKind.String)
                {
                    return detailProp.GetString();
                }

                if (root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
                {
                    return titleProp.GetString();
                }
            }
            else if (root.ValueKind == JsonValueKind.String)
            {
                return root.GetString();
            }

            return payload;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
