using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs.Seasons;
using Fc25Draft.Web.Models.Calendar;

namespace Fc25Draft.Web.Services;

public sealed class SeasonApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public SeasonApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IReadOnlyList<SeasonDto>> GetSeasonsAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync("api/seasons", ct);
        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<SeasonDto>>(response, ct) ?? Array.Empty<SeasonDto>();
    }

    public async Task<IReadOnlyList<CompetitionDto>> GetCompetitionsAsync(Guid seasonId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/seasons/{seasonId}/competitions", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Temporada não encontrada.");
        }

        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<CompetitionDto>>(response, ct) ?? Array.Empty<CompetitionDto>();
    }

    public async Task<IReadOnlyList<RoundDto>> GetRoundsAsync(Guid competitionId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/competitions/{competitionId}/rounds", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Competição não encontrada.");
        }

        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<RoundDto>>(response, ct) ?? Array.Empty<RoundDto>();
    }

    public async Task<IReadOnlyList<SeasonScheduleEntryDto>> GetScheduleAsync(Guid seasonId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/seasons/{seasonId}/schedule", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Temporada não encontrada.");
        }

        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<SeasonScheduleEntryDto>>(response, ct) ?? Array.Empty<SeasonScheduleEntryDto>();
    }

    public async Task<SeasonDto> CreateSeasonAsync(SeasonUpsertRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PostAsJsonAsync("api/seasons", request, ct);
        await EnsureSuccessAsync(response);
        return (await ReadAsync<SeasonDto>(response, ct))!;
    }

    public async Task<SeasonDto?> UpdateSeasonAsync(Guid seasonId, SeasonUpsertRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PutAsJsonAsync($"api/seasons/{seasonId}", request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await ReadAsync<SeasonDto>(response, ct);
    }

    public async Task DeleteSeasonAsync(Guid seasonId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.DeleteAsync($"api/seasons/{seasonId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Temporada não encontrada.");
        }

        await EnsureSuccessAsync(response);
    }

    public async Task<CompetitionDto> CreateCompetitionAsync(Guid seasonId, CompetitionUpsertRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PostAsJsonAsync($"api/seasons/{seasonId}/competitions", request, ct);
        await EnsureSuccessAsync(response);
        return (await ReadAsync<CompetitionDto>(response, ct))!;
    }

    public async Task<CompetitionDto?> UpdateCompetitionAsync(Guid competitionId, CompetitionUpsertRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PutAsJsonAsync($"api/competitions/{competitionId}", request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await ReadAsync<CompetitionDto>(response, ct);
    }

    public async Task DeleteCompetitionAsync(Guid competitionId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.DeleteAsync($"api/competitions/{competitionId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Competição não encontrada.");
        }

        await EnsureSuccessAsync(response);
    }

    public async Task<RoundDto> CreateRoundAsync(Guid competitionId, RoundUpsertRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PostAsJsonAsync($"api/competitions/{competitionId}/rounds", request, ct);
        await EnsureSuccessAsync(response);
        return (await ReadAsync<RoundDto>(response, ct))!;
    }

    public async Task<RoundDto?> UpdateRoundAsync(Guid roundId, RoundUpsertRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PutAsJsonAsync($"api/rounds/{roundId}", request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await ReadAsync<RoundDto>(response, ct);
    }

    public async Task<RoundDto?> CompleteRoundAsync(Guid roundId, RoundCompletionRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PostAsJsonAsync($"api/rounds/{roundId}/complete", request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await ReadAsync<RoundDto>(response, ct);
    }

    public async Task DeleteRoundAsync(Guid roundId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.DeleteAsync($"api/rounds/{roundId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Rodada não encontrada.");
        }

        await EnsureSuccessAsync(response);
    }

    public async Task<IReadOnlyList<SeasonScheduleEntryDto>> UpdateScheduleAsync(Guid seasonId, SeasonScheduleUpdateRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PutAsJsonAsync($"api/seasons/{seasonId}/schedule", request, ct);
        await EnsureSuccessAsync(response);
        return await ReadAsync<IReadOnlyList<SeasonScheduleEntryDto>>(response, ct) ?? Array.Empty<SeasonScheduleEntryDto>();
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

    private static async Task<string> ExtractErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
            if (!string.IsNullOrWhiteSpace(error?.Detail))
            {
                return error.Detail!;
            }

            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return error.Message!;
            }
        }
        catch
        {
            // ignore
        }

        return $"Erro ao comunicar com o servidor ({(int)response.StatusCode}).";
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
        }
        catch
        {
            return default;
        }
    }

    private sealed record ApiErrorResponse(string? Message, string? Detail);
}
