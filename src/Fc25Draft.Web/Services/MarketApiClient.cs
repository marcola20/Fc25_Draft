using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Web.Models.Market;

namespace Fc25Draft.Web.Services;

public class MarketApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public MarketApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IReadOnlyList<MarketItemDto>> GetActiveItemsAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);

        using var req = new HttpRequestMessage(HttpMethod.Get, "api/market");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(req, ct);

        if (!response.IsSuccessStatusCode)
        {
            await EnsureSuccessAsync(response); 
            return Array.Empty<MarketItemDto>(); 
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (!string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mediaType, "text/json", StringComparison.OrdinalIgnoreCase))
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"A API /api/market retornou conteúdo não-JSON ({mediaType ?? "desconhecido"}). " +
                $"Prévia: {text?.Substring(0, Math.Min(200, text?.Length ?? 0))}");
        }

        try
        {
            // Desserialização resiliente
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var items = await JsonSerializer.DeserializeAsync<IReadOnlyList<MarketItemDto>>(
                stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, ct);

            return items ?? Array.Empty<MarketItemDto>();
        }
        catch (JsonException jex)
        {
            var preview = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException(
                $"Falha ao ler JSON de /api/market. Prévia do corpo: {preview.Substring(0, Math.Min(200, preview.Length))}",
                jex);
        }
    }

    public async Task<PagedResult<MarketTransactionDto>> GetHistoryAsync(MarketHistoryQueryOptions query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var url = BuildHistoryUrl("api/admin/market/history", query, includePaging: true);
        using var response = await client.GetAsync(url, ct);
        await EnsureSuccessAsync(response);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var result = await JsonSerializer.DeserializeAsync<PagedResult<MarketTransactionDto>>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }, ct);

        return result ?? PagedResult<MarketTransactionDto>.Empty(query.Page, query.PageSize);
    }

    public string GetHistoryExportUrl(MarketHistoryQueryOptions query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var basePath = "api/admin/market/history/export";
        var qs = query.ToQueryString(includePaging: false);
        return string.IsNullOrEmpty(qs) ? basePath : $"{basePath}?{qs}";
    }

    public async Task<FileDownloadResult> DownloadHistoryCsvAsync(MarketHistoryQueryOptions query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        var url = BuildHistoryUrl("api/admin/market/history/export", query, includePaging: false);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        await EnsureSuccessAsync(response);

        var content = await response.Content.ReadAsByteArrayAsync(ct);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "text/csv";
        var disposition = response.Content.Headers.ContentDisposition;
        var fileName = disposition?.FileNameStar ?? disposition?.FileName?.Trim('"') ?? $"historico-mercado-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv";

        return new FileDownloadResult(content, fileName, contentType);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        string? body = null;
        string? contentType = response.Content.Headers.ContentType?.ToString();

        ApiMessageResponse? error = null;
        try
        {
            if (contentType?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true)
                error = await response.Content.ReadFromJsonAsync<ApiMessageResponse>();
        }
        catch { /* ignora */ }

        if (error is null)
        {
            try
            {
                var raw = await response.Content.ReadAsStringAsync();
                body = raw?.Length > 2000 ? raw[..2000] + "…" : raw;
            }
            catch { /* ignora */ }
        }

        var message = error?.Message
            ?? $"Erro ao comunicar com o servidor ({(int)response.StatusCode} {response.StatusCode}). " +
               (string.IsNullOrWhiteSpace(body) ? "" : $"Corpo: {body}");

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            message = "Ação permitida somente para administradores. Verifique o token informado.";

        throw new InvalidOperationException(message);
    }

    private static string BuildHistoryUrl(string basePath, MarketHistoryQueryOptions query, bool includePaging)
    {
        var qs = query.ToQueryString(includePaging);
        return string.IsNullOrEmpty(qs) ? basePath : $"{basePath}?{qs}";
    }

    private sealed record ApiMessageResponse(string? Message);

    public sealed record FileDownloadResult(byte[] Content, string FileName, string ContentType);
}
