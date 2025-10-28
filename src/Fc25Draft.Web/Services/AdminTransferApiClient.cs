using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Microsoft.AspNetCore.WebUtilities;

namespace Fc25Draft.Web.Services;

public class AdminTransferApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public AdminTransferApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<TransferResult> SellAsync(AdminSellRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/sell", request, ct);
        return await HandleResponseAsync<TransferResult>(response, ct);
    }

    public async Task<TransferResult> SwapAsync(AdminSwapRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/swap", request, ct);
        return await HandleResponseAsync<TransferResult>(response, ct);
    }

    public async Task<TransferResult> MoveAsync(AdminMoveRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/transfer/move", request, ct);
        return await HandleResponseAsync<TransferResult>(response, ct);
    }

    public async Task<AdjustBudgetResult> AdjustBudgetAsync(AdminAdjustBudgetRequest request, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.PostAsJsonAsync("api/admin/teams/adjust-budget", request, ct);
        return await HandleResponseAsync<AdjustBudgetResult>(response, ct);
    }

    public async Task<CancelItemResult> CancelMarketItemAsync(Guid itemId, string reason, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var payload = new CancelMarketItemRequest(reason);
        var response = await client.PostAsJsonAsync($"api/admin/market/cancel/{itemId}", payload, ct);
        return await HandleResponseAsync<CancelItemResult>(response, ct);
    }

    public async Task<PagedResult<TransferHistoryDto>> GetHistoryAsync(TransfersFilter filter, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        var query = new Dictionary<string, string?>
        {
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        };

        if (filter.TeamId.HasValue)
        {
            query["teamId"] = filter.TeamId.Value.ToString();
        }

        if (filter.PlayerId.HasValue)
        {
            query["playerId"] = filter.PlayerId.Value.ToString();
        }

        if (filter.Type.HasValue)
        {
            query["type"] = filter.Type.Value.ToString();
        }

        if (filter.FromUtc.HasValue)
        {
            query["from"] = filter.FromUtc.Value.ToString("o");
        }

        if (filter.ToUtc.HasValue)
        {
            query["to"] = filter.ToUtc.Value.ToString("o");
        }

        var url = QueryHelpers.AddQueryString("api/transfers/history", query);
        var response = await client.GetAsync(url, ct);
        return await HandleResponseAsync<PagedResult<TransferHistoryDto>>(response, ct);
    }

    private static async Task<T> HandleResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            if (result is null)
            {
                throw new InvalidOperationException("Resposta inesperada do servidor.");
            }

            return result;
        }

        ApiErrorResponse? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: ct);
        }
        catch
        {
            // ignorado
        }

        var message = error?.Message ?? $"Erro ao comunicar com o servidor ({response.StatusCode}).";

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            message = "Operação permitida apenas para administradores. Confirme o token informado.";
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            throw new InvalidOperationException(message);
        }

        throw new InvalidOperationException(message);
    }

    private sealed record ApiErrorResponse(string? Message);

    public sealed record AdminSellRequest(Guid FromTeamId, Guid ToTeamId, IReadOnlyCollection<Guid> PlayerIds, decimal Amount, string Reason);
    public sealed record AdminSwapRequest(Guid TeamAId, IReadOnlyCollection<Guid> PlayersFromA, Guid TeamBId, IReadOnlyCollection<Guid> PlayersFromB, decimal CashAdjustFromAToB, string Reason);
    public sealed record AdminMoveRequest(Guid PlayerId, Guid ToTeamId, string Reason);
    public sealed record AdminAdjustBudgetRequest(Guid TeamId, decimal Delta, string Reason);
    private sealed record CancelMarketItemRequest(string Reason);
}
