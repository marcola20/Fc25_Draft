using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

public class MarketApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public MarketApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IReadOnlyList<MarketItemDto>> GetActiveItemsAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.GetAsync("api/market", ct);
        await EnsureSuccessAsync(response);

        var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<MarketItemDto>>(cancellationToken: ct);
        return items ?? Array.Empty<MarketItemDto>();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiMessageResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
        }
        catch
        {
            // ignorar erros de desserialização
        }

        var message = error?.Message ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).";
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            message = "Ação permitida somente para administradores. Verifique o token informado.";
        }

        throw new InvalidOperationException(message);
    }

    private sealed record ApiMessageResponse(string? Message);
}
