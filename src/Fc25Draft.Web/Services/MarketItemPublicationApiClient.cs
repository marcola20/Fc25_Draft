using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Fc25Draft.Web.Services;

public class MarketItemPublicationApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public MarketItemPublicationApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IReadOnlyList<MarketItemPublicationDto>> ListAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.GetAsync("api/market/items", ct);

        await EnsureSuccessAsync(response);

        var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<MarketItemPublicationDto>>(cancellationToken: ct);
        return items ?? Array.Empty<MarketItemPublicationDto>();
    }

    public async Task<MarketItemPublicationDto?> GetAsync(Guid itemId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.GetAsync($"api/market/items/{itemId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<MarketItemPublicationDto>(cancellationToken: ct);
    }

    public async Task<MarketItemPublicationDto> CreateDraftAsync(MarketItemDraftCreateRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/market/items", request, ct);

        await EnsureSuccessAsync(response);

        var created = await response.Content.ReadFromJsonAsync<MarketItemPublicationDto>(cancellationToken: ct);
        if (created is null)
        {
            throw new InvalidOperationException("Resposta inválida do servidor ao criar item de mercado.");
        }

        return created;
    }

    public async Task<MarketItemPublicationDto> UpdateDraftAsync(Guid itemId, MarketItemDraftUpdateRequest request, uint rowVersion, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var message = new HttpRequestMessage(HttpMethod.Put, $"api/market/items/{itemId}")
        {
            Content = JsonContent.Create(request)
        };

        message.Headers.IfMatch.Clear();
        message.Headers.IfMatch.Add(EntityTagHeaderValue.Parse($"W/\"{rowVersion}\""));

        var response = await client.SendAsync(message, ct);

        await EnsureSuccessAsync(response);

        var updated = await response.Content.ReadFromJsonAsync<MarketItemPublicationDto>(cancellationToken: ct);
        if (updated is null)
        {
            throw new InvalidOperationException("Resposta inválida do servidor ao atualizar item de mercado.");
        }

        return updated;
    }

    public async Task<MarketItemPublicationDto> PublishAsync(Guid itemId, uint rowVersion, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"api/market/items/{itemId}/publish");

        message.Headers.IfMatch.Clear();
        message.Headers.IfMatch.Add(EntityTagHeaderValue.Parse($"W/\"{rowVersion}\""));

        var response = await client.SendAsync(message, ct);

        await EnsureSuccessAsync(response);

        var published = await response.Content.ReadFromJsonAsync<MarketItemPublicationDto>(cancellationToken: ct);
        if (published is null)
        {
            throw new InvalidOperationException("Resposta inválida do servidor ao publicar item de mercado.");
        }

        return published;
    }

    public async Task DeleteAsync(Guid itemId, uint rowVersion, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var message = new HttpRequestMessage(HttpMethod.Delete, $"api/market/items/{itemId}");

        message.Headers.IfMatch.Clear();
        message.Headers.IfMatch.Add(EntityTagHeaderValue.Parse($"W/\"{rowVersion}\""));

        var response = await client.SendAsync(message, ct);

        await EnsureSuccessAsync(response);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ProblemDetails? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        }
        catch
        {
            // Ignored: fallback message below
        }

        string message = problem?.Detail ?? problem?.Title ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).";

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            message = "Ação permitida somente para administradores. Verifique o token informado.";
        }

        throw new InvalidOperationException(message);
    }
}
