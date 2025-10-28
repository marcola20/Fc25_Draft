using System;
using System.Collections.Generic;
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

    public async Task<string> SellPlayersAsync(AdminSellPlayersRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/sell", request, ct);
        return await ReadSuccessMessageAsync(response, ct);
    }

    public async Task<string> SwapPlayersAsync(AdminSwapPlayersRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/swap", request, ct);
        return await ReadSuccessMessageAsync(response, ct);
    }

    public async Task<string> MovePlayerAsync(AdminMovePlayerRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/move", request, ct);
        return await ReadSuccessMessageAsync(response, ct);
    }

    public async Task<string> AdjustBudgetAsync(AdminAdjustBudgetRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/teams/adjust-budget", request, ct);
        return await ReadSuccessMessageAsync(response, ct);
    }

    public async Task<string> CancelMarketItemAsync(Guid itemId, AdminCancelMarketItemRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync($"api/admin/market/cancel/{itemId}", request, ct);
        return await ReadSuccessMessageAsync(response, ct);
    }

    public async Task<decimal> GetTeamBudgetAsync(Guid teamId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        var response = await client.GetAsync($"api/budgets/{teamId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException("Time não encontrado.");
        }

        await EnsureSuccessAsync(response, ct);

        var payload = await response.Content.ReadFromJsonAsync<BudgetResponse>(cancellationToken: ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Resposta inválida ao consultar orçamento.");
        }

        return payload.Saldo;
    }

    public async Task<IReadOnlyList<MarketItemDto>> GetActiveMarketItemsAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.GetAsync("api/market", ct);

        await EnsureSuccessAsync(response, ct);

        var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<MarketItemDto>>(cancellationToken: ct);
        return items ?? Array.Empty<MarketItemDto>();
    }

    private static async Task<string> ReadSuccessMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);

        var payload = await response.Content.ReadFromJsonAsync<SuccessResponse>(cancellationToken: ct);
        return payload?.Message ?? "Operação concluída com sucesso.";
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
            // Ignored
        }

        var message = error?.Message ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).";

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            message = "Ação permitida somente para administradores. Verifique o token informado.";
        }

        throw new InvalidOperationException(message);
    }

    private sealed record SuccessResponse(string? Message);

    private sealed record ApiErrorResponse(string? Message);

    private sealed record BudgetResponse(Guid TeamId, decimal Saldo);
}
