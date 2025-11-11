using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.DTOs.Seasons;
using Fc25Draft.Web.Models.Calendar;

namespace Fc25Draft.Web.Services;

public sealed class RoundSelectionApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public RoundSelectionApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<RoundSelectionDto> GetAsync(Guid roundId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"api/rounds/{roundId}/selection", ct);

        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct) ?? "Falha ao carregar a seleção da rodada.";
            throw new InvalidOperationException(message);
        }

        var dto = await response.Content.ReadFromJsonAsync<RoundSelectionDto>(cancellationToken: ct);
        return dto ?? new RoundSelectionDto(roundId, Array.Empty<RoundSelectionPlayerDto>());
    }

    public async Task<OperationResultDto> AddPlayersAsync(Guid roundId, IEnumerable<Guid> playerIds, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var payload = new RoundSelectionPlayersRequest
        {
            PlayerIds = playerIds?.ToArray() ?? Array.Empty<Guid>()
        };

        using var response = await client.PostAsJsonAsync($"api/rounds/{roundId}/selection/players", payload, ct);

        return await ReadOperationResultAsync(response, "Falha ao atualizar a seleção da rodada.", ct);
    }

    public async Task<OperationResultDto> RemovePlayerAsync(Guid roundId, Guid playerId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.DeleteAsync($"api/rounds/{roundId}/selection/players/{playerId}", ct);

        return await ReadOperationResultAsync(response, "Falha ao atualizar a seleção da rodada.", ct);
    }

    private static async Task<OperationResultDto> ReadOperationResultAsync(HttpResponseMessage response, string defaultMessage, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response, ct) ?? defaultMessage;
            return new OperationResultDto(false, message);
        }

        var result = await response.Content.ReadFromJsonAsync<OperationResultDto>(cancellationToken: ct);
        return result ?? new OperationResultDto(true, "Seleção atualizada com sucesso.");
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
