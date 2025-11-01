using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Fc25Draft.Web.Models.MarketCycles;
using System.Linq;
using System.Collections.Generic;

namespace Fc25Draft.Web.Services;

public interface IMarketItemGenerationClient
{
    Task<MarketItemGenerationPreviewDto> PreviewAsync(Guid cycleId, MarketItemGenerationRequestDto request, CancellationToken ct);

    Task<MarketItemGenerationResultDto> GenerateAsync(Guid cycleId, MarketItemGenerationRequestDto request, CancellationToken ct);

    Task<MarketItemGenerationDeleteResultDto> DeleteDraftsAsync(Guid cycleId, CancellationToken ct);
}

public class MarketItemGenerationClient : IMarketItemGenerationClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly ApiClientFactory _clientFactory;

    public MarketItemGenerationClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<MarketItemGenerationPreviewDto> PreviewAsync(Guid cycleId, MarketItemGenerationRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await _clientFactory.CreateAsync(includeAdminToken: true).ConfigureAwait(false);
        using var response = await client.PostAsJsonAsync($"api/admin/market/cycles/{cycleId}/items:preview", request, SerializerOptions, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return await ReadJsonAsync<MarketItemGenerationPreviewDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Resposta inválida do servidor ao pré-visualizar a geração de itens.");
    }

    public async Task<MarketItemGenerationResultDto> GenerateAsync(Guid cycleId, MarketItemGenerationRequestDto request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await _clientFactory.CreateAsync(includeAdminToken: true).ConfigureAwait(false);
        using var response = await client.PostAsJsonAsync($"api/admin/market/cycles/{cycleId}/items:generate", request, SerializerOptions, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return await ReadJsonAsync<MarketItemGenerationResultDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Resposta inválida do servidor ao gerar itens para o ciclo.");
    }

    public async Task<MarketItemGenerationDeleteResultDto> DeleteDraftsAsync(Guid cycleId, CancellationToken ct)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true).ConfigureAwait(false);
        using var response = await client.DeleteAsync($"api/admin/market/cycles/{cycleId}/items", ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
        return await ReadJsonAsync<MarketItemGenerationDeleteResultDto>(response, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Resposta inválida do servidor ao limpar itens gerados do ciclo.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var details = await BuildErrorDetailsAsync(response, ct).ConfigureAwait(false);
        throw new HttpRequestException(details.UserMessage, null, response.StatusCode);
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException($"Resposta não está em JSON (Content-Type: {contentType ?? "desconhecido"}).\nBody (primeiros 2000 chars):\n{(raw?.Length > 2000 ? raw[..2000] + "…(truncated)" : raw)}");
        }

        return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, ct).ConfigureAwait(false);
    }

    private static async Task<(string UserMessage, string FullLog)> BuildErrorDetailsAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var method = response.RequestMessage?.Method.Method ?? "UNKNOWN";
        var url = response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN";
        var status = (int)response.StatusCode;
        var reason = response.ReasonPhrase ?? string.Empty;

        string raw = string.Empty;
        try
        {
            raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // ignorado
        }

        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(SerializerOptions, ct).ConfigureAwait(false);
            if (error is not null)
            {
                var message = !string.IsNullOrWhiteSpace(error.Message)
                    ? error.Message
                    : error.Errors is { Count: > 0 }
                        ? string.Join(" ", error.Errors.SelectMany(pair => pair.Value ?? Array.Empty<string>()))
                        : null;

                if (!string.IsNullOrWhiteSpace(message))
                {
                    return (Decorate(message), ComposeLog(raw));
                }
            }
        }
        catch
        {
            // fallback
        }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ct).ConfigureAwait(false);
            if (problem is not null)
            {
                var message = problem.Title ?? problem.Detail ?? "Erro inesperado.";
                return (Decorate(message), ComposeLog(problem.Detail ?? raw));
            }
        }
        catch
        {
            // fallback
        }

        var fallback = $"{method} {url} -> {status} {reason}.";
        return (Decorate("Falha ao comunicar com o servidor."), ComposeLog(fallback + " Corpo: " + raw));

        string ComposeLog(string? body)
            => $"{method} {url} -> {status} {reason}. Body: {body ?? raw}";

        static string Decorate(string message)
            => string.IsNullOrWhiteSpace(message)
                ? "Falha ao comunicar com o servidor."
                : message.Trim();
    }

    private sealed record ApiErrorResponse(string? Message, Dictionary<string, string[]?>? Errors);
}
