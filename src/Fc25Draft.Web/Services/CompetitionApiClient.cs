using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs.Competitions;
using Fc25Draft.Web.Models.Competitions;
using Microsoft.AspNetCore.Mvc;

namespace Fc25Draft.Web.Services;

public sealed class CompetitionApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public CompetitionApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IReadOnlyList<CompetitionSummaryDto>> GetCompetitionsAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync("api/competition-module/competitions", ct);
        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<CompetitionSummaryDto>>(response, ct) ?? Array.Empty<CompetitionSummaryDto>();
    }

    public async Task<CompetitionDetailsDto> GetCompetitionDetailsAsync(Guid competitionId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/competition-module/competitions/{competitionId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Competição não encontrada.");
        }

        await EnsureSuccessAsync(response);
        return (await ReadAsync<CompetitionDetailsDto>(response, ct))!;
    }

    public async Task<CompetitionSummaryDto> CreateCompetitionAsync(CompetitionCreateRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PostAsJsonAsync("api/competition-module/competitions", request, ct);
        await EnsureSuccessAsync(response);
        return (await ReadAsync<CompetitionSummaryDto>(response, ct))!;
    }

    public async Task<CompetitionSummaryDto?> UpdateCompetitionAsync(Guid competitionId, CompetitionUpdateRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PutAsJsonAsync($"api/competition-module/competitions/{competitionId}", request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await ReadAsync<CompetitionSummaryDto>(response, ct);
    }

    public async Task ToggleCompetitionAsync(Guid competitionId, bool isActive, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PostAsJsonAsync($"api/competition-module/competitions/{competitionId}/activate", new CompetitionToggleRequest { IsActive = isActive }, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Competição não encontrada.");
        }

        await EnsureSuccessAsync(response);
    }

    public async Task<IReadOnlyList<CompetitionTeamDto>> GetTeamsAsync(Guid competitionId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/competition-module/competitions/{competitionId}/teams", ct);
        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<CompetitionTeamDto>>(response, ct) ?? Array.Empty<CompetitionTeamDto>();
    }

    public async Task<CompetitionTeamDto> AddTeamAsync(Guid competitionId, CompetitionTeamRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PostAsJsonAsync($"api/competition-module/competitions/{competitionId}/teams", request, ct);
        await EnsureSuccessAsync(response);
        return (await ReadAsync<CompetitionTeamDto>(response, ct))!;
    }

    public async Task RemoveTeamAsync(Guid competitionId, Guid competitionTeamId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.DeleteAsync($"api/competition-module/competitions/{competitionId}/teams/{competitionTeamId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Time não encontrado na competição.");
        }

        await EnsureSuccessAsync(response);
    }

    public async Task<IReadOnlyList<CompetitionRoundDto>> GetRoundsAsync(Guid competitionId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/competition-module/competitions/{competitionId}/rounds", ct);
        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<CompetitionRoundDto>>(response, ct) ?? Array.Empty<CompetitionRoundDto>();
    }

    public async Task<IReadOnlyList<CompetitionRoundDto>> GenerateRoundsAsync(Guid competitionId, CompetitionRoundGenerationRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PostAsJsonAsync($"api/competition-module/competitions/{competitionId}/rounds/generate", request, ct);
        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<CompetitionRoundDto>>(response, ct) ?? Array.Empty<CompetitionRoundDto>();
    }

    public async Task<IReadOnlyList<CompetitionStandingDto>> GetStandingsAsync(Guid competitionId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/competition-module/competitions/{competitionId}/standings", ct);
        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<CompetitionStandingDto>>(response, ct) ?? Array.Empty<CompetitionStandingDto>();
    }

    public async Task<IReadOnlyList<CompetitionStandingDto>> RebuildStandingsAsync(Guid competitionId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PostAsync($"api/competition-module/competitions/{competitionId}/rebuild", null, ct);
        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<CompetitionStandingDto>>(response, ct) ?? Array.Empty<CompetitionStandingDto>();
    }

    public async Task<IReadOnlyList<CompetitionPlayerStatDto>> GetPlayerStatsAsync(Guid competitionId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/competition-module/competitions/{competitionId}/player-stats", ct);
        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<CompetitionPlayerStatDto>>(response, ct) ?? Array.Empty<CompetitionPlayerStatDto>();
    }

    public async Task<IReadOnlyList<CompetitionTeamStatDto>> GetTeamStatsAsync(Guid competitionId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/competition-module/competitions/{competitionId}/team-stats", ct);
        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<CompetitionTeamStatDto>>(response, ct) ?? Array.Empty<CompetitionTeamStatDto>();
    }

    public async Task<CompetitionMatchDetailsDto?> GetMatchAsync(Guid competitionMatchId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/competition-module/matches/{competitionMatchId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await ReadAsync<CompetitionMatchDetailsDto>(response, ct);
    }

    public async Task<CompetitionMatchDetailsDto> UpsertMatchAsync(CompetitionMatchUpsertRequest request, CancellationToken ct = default)
    {
        var matchId = request.CompetitionMatchId ?? Guid.Empty;
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        HttpResponseMessage response;
        if (matchId == Guid.Empty)
        {
            response = await client.PostAsJsonAsync("api/competition-module/matches", request, ct);
        }
        else
        {
            response = await client.PutAsJsonAsync($"api/competition-module/matches/{matchId}", request, ct);
        }

        await EnsureSuccessAsync(response);
        return (await ReadAsync<CompetitionMatchDetailsDto>(response, ct))!;
    }

    public async Task DeleteMatchAsync(Guid competitionMatchId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.DeleteAsync($"api/competition-module/matches/{competitionMatchId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Partida não encontrada.");
        }

        await EnsureSuccessAsync(response);
    }

    public async Task<CompetitionMatchDetailsDto> ReplaceMatchEventsAsync(Guid competitionMatchId, CompetitionMatchEventsRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PutAsJsonAsync($"api/competition-module/matches/{competitionMatchId}/events", request, ct);
        await EnsureSuccessAsync(response);
        return (await ReadAsync<CompetitionMatchDetailsDto>(response, ct))!;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string message = await ExtractErrorMessageAsync(response);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            message = "Operação permitida somente para administradores.";
        }

        throw new InvalidOperationException(message);
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
        => await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct).ConfigureAwait(false);

    private static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>().ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem.Detail;
            }

            if (problem?.Errors is not null && problem.Errors.Count > 0)
            {
                return string.Join("; ", problem.Errors.SelectMany(kvp => kvp.Value ?? Array.Empty<string>()));
            }
        }
        catch
        {
            // ignored
        }

        return $"Falha ao executar operação: {(int)response.StatusCode} {response.ReasonPhrase}";
    }
}
