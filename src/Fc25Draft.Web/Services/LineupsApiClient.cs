using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

public class LineupsApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public LineupsApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IReadOnlyList<TeamLineupDto>> GetLineupsAsync(Guid teamId, string teamToken, CancellationToken ct = default)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        var client = await CreateClientWithTokenAsync(teamToken);
        var response = await client.GetAsync($"api/teams/{teamId}/lineups", ct);
        await EnsureSuccessAsync(response, ct);

        var payload = await response.Content.ReadFromJsonAsync<IReadOnlyList<TeamLineupDto>>(cancellationToken: ct);
        return payload ?? Array.Empty<TeamLineupDto>();
    }

    public async Task<TeamLineupDto> CreateAsync(Guid teamId, TeamLineupSaveRequestDto request, string teamToken, CancellationToken ct = default)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var client = await CreateClientWithTokenAsync(teamToken);
        var response = await client.PostAsJsonAsync($"api/teams/{teamId}/lineups", request, ct);
        await EnsureSuccessAsync(response, ct);

        var payload = await response.Content.ReadFromJsonAsync<TeamLineupDto>(cancellationToken: ct);
        return payload ?? throw new InvalidOperationException("Resposta inválida do servidor.");
    }

    public async Task<TeamLineupDto> UpdateAsync(Guid teamId, Guid lineupId, TeamLineupSaveRequestDto request, string teamToken, CancellationToken ct = default)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (lineupId == Guid.Empty)
        {
            throw new ArgumentException("Escalação inválida.", nameof(lineupId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var client = await CreateClientWithTokenAsync(teamToken);
        var response = await client.PutAsJsonAsync($"api/teams/{teamId}/lineups/{lineupId}", request, ct);
        await EnsureSuccessAsync(response, ct);

        var payload = await response.Content.ReadFromJsonAsync<TeamLineupDto>(cancellationToken: ct);
        return payload ?? throw new InvalidOperationException("Resposta inválida do servidor.");
    }

    public async Task DeleteAsync(Guid teamId, Guid lineupId, string teamToken, CancellationToken ct = default)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (lineupId == Guid.Empty)
        {
            throw new ArgumentException("Escalação inválida.", nameof(lineupId));
        }

        var client = await CreateClientWithTokenAsync(teamToken);
        var response = await client.DeleteAsync($"api/teams/{teamId}/lineups/{lineupId}", ct);
        await EnsureSuccessAsync(response, ct);
    }

    public async Task ActivateAsync(Guid teamId, Guid lineupId, string teamToken, CancellationToken ct = default)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (lineupId == Guid.Empty)
        {
            throw new ArgumentException("Escalação inválida.", nameof(lineupId));
        }

        var client = await CreateClientWithTokenAsync(teamToken);
        var response = await client.PostAsync($"api/teams/{teamId}/lineups/{lineupId}/activate", null, ct);
        await EnsureSuccessAsync(response, ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: ct);
        }
        catch
        {
            // ignore
        }

        var message = error?.Message ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).";

        throw response.StatusCode switch
        {
            HttpStatusCode.NotFound => new KeyNotFoundException(message),
            HttpStatusCode.BadRequest => new InvalidOperationException(message),
            HttpStatusCode.Conflict => new InvalidOperationException(message),
            _ => new InvalidOperationException(message)
        };
    }

    private sealed record ApiErrorResponse(string? Message);

    private async Task<HttpClient> CreateClientWithTokenAsync(string teamToken)
    {
        if (string.IsNullOrWhiteSpace(teamToken))
        {
            throw new InvalidOperationException("Token do time é obrigatório.");
        }

        var client = await _clientFactory.CreateAsync();
        client.DefaultRequestHeaders.Remove("X-Team-Token");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());
        return client;
    }
}
