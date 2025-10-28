using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

public class AdminTransfersApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public AdminTransfersApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<string> SellAsync(AdminSellPlayersRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/sell", request, ct);
        return await ReadMessageAsync(response);
    }

    public async Task<string> SwapAsync(AdminSwapPlayersRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/swap", request, ct);
        return await ReadMessageAsync(response);
    }

    public async Task<string> MoveAsync(AdminMovePlayerRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/move", request, ct);
        return await ReadMessageAsync(response);
    }

    public async Task<string> AdjustBudgetAsync(AdminAdjustBudgetRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/teams/adjust-budget", request, ct);
        return await ReadMessageAsync(response);
    }

    public async Task<string> CancelMarketItemAsync(Guid itemId, AdminCancelMarketItemRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync($"api/admin/market/cancel/{itemId}", request, ct);
        return await ReadMessageAsync(response);
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            var success = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
            return success?.Message ?? "Operação concluída com sucesso.";
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
