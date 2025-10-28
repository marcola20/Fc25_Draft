using System.Net;
using System.Text.Json;

namespace Fc25Draft.Web.Services;

public class BudgetsApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public BudgetsApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<decimal?> GetSaldoAsync(Guid teamId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var response = await client.GetAsync($"api/budgets/{teamId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (document.RootElement.TryGetProperty("saldo", out var saldoProperty) && saldoProperty.TryGetDecimal(out var saldo))
        {
            return saldo;
        }

        throw new InvalidOperationException("Resposta inválida ao obter saldo do time.");
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
            var stream = await response.Content.ReadAsStreamAsync();
            error = await JsonSerializer.DeserializeAsync<ApiErrorResponse>(stream);
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

    private sealed record ApiErrorResponse(string? Message);
}
