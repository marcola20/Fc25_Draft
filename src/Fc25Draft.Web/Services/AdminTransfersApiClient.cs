using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
        await EnsureSuccessAsync(response);
        return await ReadMessageAsync(response, ct);
    }

    public async Task<string> SwapAsync(AdminSwapPlayersRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/swap", request, ct);
        await EnsureSuccessAsync(response);
        return await ReadMessageAsync(response, ct);
    }

    public async Task<string> MoveAsync(AdminMovePlayerRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/move", request, ct);
        await EnsureSuccessAsync(response);
        return await ReadMessageAsync(response, ct);
    }

    public async Task<string> AdjustBudgetAsync(AdminAdjustBudgetRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/teams/adjust-budget", request, ct);
        await EnsureSuccessAsync(response);
        return await ReadMessageAsync(response, ct);
    }

    public async Task<string> CancelMarketItemAsync(Guid itemId, AdminCancelMarketItemRequestDto request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync($"api/admin/market/cancel/{itemId}", request, ct);
        await EnsureSuccessAsync(response);
        return await ReadMessageAsync(response, ct);
    }

    public async Task<IReadOnlyList<MarketItemDto>> GetActiveMarketItemsAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        var response = await client.GetAsync("api/market", ct);
        await EnsureSuccessAsync(response);
        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<MarketItemDto>>(cancellationToken: ct);
        return result ?? Array.Empty<MarketItemDto>();
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
            // ignored
        }

        var message = error?.Message ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).";

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            message = "Ação permitida somente para administradores. Verifique o token informado.";
        }

        throw new InvalidOperationException(message);
    }

    private static async Task<string> ReadMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        MessageResponse? payload = null;
        try
        {
            payload = await response.Content.ReadFromJsonAsync<MessageResponse>(cancellationToken: ct);
        }
        catch
        {
            // ignored
        }

        return string.IsNullOrWhiteSpace(payload?.Message) ? "Operação concluída com sucesso." : payload!.Message!;
    }

    private sealed record ApiErrorResponse(string? Message);
    private sealed record MessageResponse(string? Message);
}
