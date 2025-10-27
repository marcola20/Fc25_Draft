using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

public class DraftAdminApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public DraftAdminApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IReadOnlyList<DraftSummaryDto>> GetDraftsAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.GetAsync("api/admin/draft", ct);

        await EnsureSuccessAsync(response);

        var drafts = await response.Content.ReadFromJsonAsync<IReadOnlyList<DraftSummaryDto>>(cancellationToken: ct);
        return drafts ?? Array.Empty<DraftSummaryDto>();
    }

    public async Task<DraftDetailsDto?> GetDraftAsync(Guid draftId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.GetAsync($"api/admin/draft/{draftId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<DraftDetailsDto>(cancellationToken: ct);
    }

    public async Task<DraftRoundDetailsDto> AddRoundAsync(Guid draftId, DraftRoundCreateDto? request, CancellationToken ct = default)
    {
        request ??= new DraftRoundCreateDto(null, null);

        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync($"api/admin/draft/{draftId}/rounds", request, ct);

        await EnsureSuccessAsync(response);
        var round = await response.Content.ReadFromJsonAsync<DraftRoundDetailsDto>(cancellationToken: ct);
        if (round is null)
        {
            throw new InvalidOperationException("Resposta inválida do servidor ao criar rodada.");
        }

        return round;
    }

    public async Task DeleteRoundAsync(Guid draftId, int roundNumber, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.DeleteAsync($"api/admin/draft/{draftId}/rounds/{roundNumber}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException("Rodada não encontrada.");
        }

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
            // Ignored
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
