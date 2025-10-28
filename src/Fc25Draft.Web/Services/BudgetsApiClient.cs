using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

public class BudgetsApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public BudgetsApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<BudgetSummaryDto?> GetSummaryAsync(Guid teamId, CancellationToken ct = default)
    {
        if (teamId == Guid.Empty)
        {
            return null;
        }

        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.GetAsync($"api/budgets/{teamId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);

        var payload = await response.Content.ReadFromJsonAsync<SaldoResponse>(cancellationToken: ct);
        return payload is null ? null : new BudgetSummaryDto(payload.TeamId, payload.Saldo, 0m, payload.Saldo);
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

    private sealed record SaldoResponse(Guid TeamId, decimal Saldo);
}
