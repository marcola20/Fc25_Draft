using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Web.Models.MarketCycles;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Fc25Draft.Web.Services;

public class MarketCycleClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ApiClientFactory _clientFactory;

    public MarketCycleClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<PagedResult<MarketCycleDto>> QueryAsync(MarketCycleQueryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await _clientFactory.CreateAsync(includeAdminToken: true).ConfigureAwait(false);
        var urlBuilder = new StringBuilder("api/admin/market/cycles");
        AppendQueryString(urlBuilder, request);

        using var response = await client.GetAsync(urlBuilder.ToString(), ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);

        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (contentType is null || !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Esperado JSON, mas veio '{contentType ?? "desconhecido"}'. " +
                $"Body (primeiros 400 chars): {raw[..Math.Min(400, raw.Length)]}");
        }

        var result = await response.Content
            .ReadFromJsonAsync<PagedResult<MarketCycleDto>>(SerializerOptions, ct)
            .ConfigureAwait(false);

        return result ?? PagedResult<MarketCycleDto>.Empty(request.Page, request.PageSize);
    }

    public async Task<MarketCycleDto?> GetByIdAsync(Guid cycleId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true).ConfigureAwait(false);
        using var response = await client.GetAsync($"api/admin/market/cycles/{cycleId}", ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<MarketCycleDto>(SerializerOptions, ct).ConfigureAwait(false);
    }

    public async Task<MarketCycleDto> CreateAsync(MarketCycleCreateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await _clientFactory.CreateAsync(includeAdminToken: true).ConfigureAwait(false);
        using var response = await client.PostAsJsonAsync("api/admin/market/cycles", request, SerializerOptions, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (!string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(contentType, "text/json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(contentType, "application/problem+json", StringComparison.OrdinalIgnoreCase))
        {
            var raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Resposta não está em JSON (Content-Type: {contentType ?? "desconhecido"}).\n" +
                $"Body (primeiros 2000 chars):\n{(raw?.Length > 2000 ? raw[..2000] + "…(truncated)" : raw)}");
        }

        var dto = await response.Content.ReadFromJsonAsync<MarketCycleDto>(SerializerOptions, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("Resposta inválida do servidor ao criar o ciclo de mercado.");
        return dto;
    }

    public async Task<MarketCycleStatusUpdateResult?> UpdateStatusAsync(Guid cycleId, MarketCycleStatus status, bool forceClose, CancellationToken ct = default)
    {
        var request = new MarketCycleStatusUpdateRequest
        {
            Status = status,
            ForceClose = forceClose
        };

        var client = await _clientFactory.CreateAsync(includeAdminToken: true).ConfigureAwait(false);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Patch, $"api/admin/market/cycles/{cycleId}/status")
        {
            Content = JsonContent.Create(request)
        };

        var idempotencyKey = Guid.NewGuid().ToString("N");
        httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        using var response = await client.SendAsync(httpRequest, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException($"500 from server while updating cycle. Body:\n{body}");
        }

        if (response.StatusCode == HttpStatusCode.Accepted)
            return null;

        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        var result = await response.Content
            .ReadFromJsonAsync<MarketCycleStatusUpdateResult>(SerializerOptions, ct)
            .ConfigureAwait(false);

        if (result is null || result.Cycle is null)
            throw new InvalidOperationException("Invalid response from server when updating cycle status.");

        return result;
    }

    private static void AppendQueryString(StringBuilder builder, MarketCycleQueryRequest request)
    {
        var hasQuery = false;
        void Append(string name, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            builder.Append(hasQuery ? '&' : '?');
            builder.Append(name);
            builder.Append('=');
            builder.Append(UrlEncoder.Default.Encode(value));
            hasQuery = true;
        }

        Append("page", request.Page.ToString());
        Append("pageSize", request.PageSize.ToString());

        if (request.Status.HasValue)
        {
            Append("status", request.Status.Value.ToString());
        }

        if (request.StartsAfterUtc.HasValue)
        {
            Append("startsAfterUtc", request.StartsAfterUtc.Value.ToString("O"));
        }

        if (request.StartsBeforeUtc.HasValue)
        {
            Append("startsBeforeUtc", request.StartsBeforeUtc.Value.ToString("O"));
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var details = await BuildErrorDetailsAsync(response, ct).ConfigureAwait(false);

        throw new HttpRequestException(details.UserMessage, null, response.StatusCode);
    }

    private static async Task<(string UserMessage, string FullLog)> BuildErrorDetailsAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var method = response.RequestMessage?.Method.Method ?? "UNKNOWN";
        var url = response.RequestMessage?.RequestUri?.ToString() ?? "UNKNOWN";
        var status = (int)response.StatusCode;
        var reason = response.ReasonPhrase ?? "";

        string raw = string.Empty;
        try { raw = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false); } catch { /* ignore */ }

        try
        {
            var err = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(SerializerOptions, ct).ConfigureAwait(false);
            if (err is not null)
            {
                var msg = !string.IsNullOrWhiteSpace(err.Message)
                    ? err.Message
                    : (err.Errors is { Count: > 0 }
                        ? string.Join(" ", err.Errors.SelectMany(kv => kv.Value ?? Array.Empty<string>()))
                        : null);

                if (!string.IsNullOrWhiteSpace(msg))
                    return (Decorate(msg), ComposeLog());
            }
        }
        catch { /* fallback */ }

        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(ct).ConfigureAwait(false);
            if (problem is not null)
            {
                var msg = problem.Title ?? problem.Detail ?? "Unexpected error.";
                return (Decorate(msg), ComposeLog(problem.Detail ?? raw));
            }
        }
        catch { /* fallback */ }

        try
        {
            var v = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(ct).ConfigureAwait(false);
            if (v is not null)
            {
                var flat = v.Errors?.Values?.SelectMany(x => x)?.ToArray() ?? Array.Empty<string>();
                var msg = flat.Length > 0 ? string.Join(" ", flat) : (v.Title ?? "Validation error.");
                return (Decorate(msg), ComposeLog(string.Join(Environment.NewLine, flat)));
            }
        }
        catch { /* fallback */ }

        if (!string.IsNullOrWhiteSpace(raw))
            return (Decorate(raw), ComposeLog());

        string fallback = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => "Ação permitida somente para administradores. Verifique o token informado.",
            HttpStatusCode.NotFound
                => "Recurso não encontrado.",
            HttpStatusCode.Conflict
                => "A operação não pôde ser concluída devido a um conflito no estado atual.",
            _ => "Falha ao comunicar com o servidor."
        };
        return (Decorate(fallback), ComposeLog());

        // Helpers locais
        string Decorate(string msg)
            => $"{msg}".Trim();

        string ComposeLog(string? bodyOverride = null)
        {
            var contentType = response.Content?.Headers?.ContentType?.ToString() ?? "(none)";
            var requestId = response.Headers.TryGetValues("x-request-id", out var v1) ? v1.FirstOrDefault()
                           : response.Headers.TryGetValues("Request-Id", out var v2) ? v2.FirstOrDefault()
                           : response.Headers.TryGetValues("traceparent", out var v3) ? v3.FirstOrDefault()
                           : null;

            var body = (bodyOverride ?? raw) ?? string.Empty;
            const int max = 4000; // evita log gigante
            if (body.Length > max) body = body[..max] + "…(truncated)";

            return $"HTTP {status} {reason} at {method} {url}\n" +
                   $"Content-Type: {contentType}\n" +
                   (requestId is null ? "" : $"RequestId: {requestId}\n") +
                   $"Body:\n{body}";
        }
    }

    private sealed record ApiErrorResponse(string? Message, Dictionary<string, string[]?>? Errors);
}
