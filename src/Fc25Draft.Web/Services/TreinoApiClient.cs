using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

/// <summary>Treino da semana: grava as escolhas do time pela API (com o token do time).</summary>
public class TreinoApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public TreinoApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task SalvarAsync(IReadOnlyList<TreinoEscolhaRequest> escolhas, string teamToken, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, "api/treino");
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());
        request.Content = JsonContent.Create(escolhas);

        using var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return;

        string? message = null;
        try { message = (await response.Content.ReadFromJsonAsync<Erro>(cancellationToken: ct))?.Message; } catch { }
        throw new InvalidOperationException(message ?? $"Erro ao salvar o treino ({response.StatusCode}).");
    }

    private sealed record Erro(string? Message);
}
