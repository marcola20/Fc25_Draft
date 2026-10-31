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

    public Task SalvarAsync(IReadOnlyList<TreinoEscolhaRequest> escolhas, string teamToken, CancellationToken ct = default) =>
        EnviarAsync(HttpMethod.Put, "api/treino", escolhas, teamToken, ct);

    public Task ApadrinharAsync(int padrinhoId, int afilhadoId, string teamToken, CancellationToken ct = default) =>
        EnviarAsync(HttpMethod.Post, "api/treino/padrinho", new ApadrinharRequest(padrinhoId, afilhadoId), teamToken, ct);

    private async Task EnviarAsync<T>(HttpMethod metodo, string url, T corpo, string teamToken, CancellationToken ct)
    {
        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(metodo, url);
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());
        request.Content = JsonContent.Create(corpo);

        using var response = await client.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return;

        string? message = null;
        try { message = (await response.Content.ReadFromJsonAsync<Erro>(cancellationToken: ct))?.Message; } catch { }
        throw new InvalidOperationException(message ?? $"Erro ao falar com o servidor ({response.StatusCode}).");
    }

    private sealed record Erro(string? Message);
}
