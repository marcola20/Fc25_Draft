using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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
        try
        {
            var client = await _clientFactory.CreateAsync(includeAdminToken: true);

            HttpResponseMessage response = null!;

            try
            {
                response = await client.GetAsync("api/market", ct);
            }
            catch (Exception httpEx)
            {
                throw new HttpRequestException($"Erro ao chamar API /api/market: {httpEx.Message}", httpEx);
            }

            if (!response.IsSuccessStatusCode)
            {
                string? content = null;
                try
                {
                    content = await response.Content.ReadAsStringAsync(ct);
                }
                catch (Exception readEx)
                {
                    content = $"[Falha ao ler conteúdo da resposta: {readEx.Message}]";
                }

                throw new HttpRequestException(
                    $"Erro HTTP {(int)response.StatusCode} {response.ReasonPhrase} ao chamar /api/market. " +
                    $"Corpo da resposta: {content}");
            }

            var items = await response.Content.ReadFromJsonAsync<IReadOnlyList<MarketItemDto>>(cancellationToken: ct);
            return items ?? Array.Empty<MarketItemDto>();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine("❌ Erro HTTP ao buscar market items:");
            Console.WriteLine(ex.ToString());
            throw; 
        }
        catch (Exception ex)
        {
            Console.WriteLine("❌ Erro inesperado em GetActiveItemsAsync:");
            Console.WriteLine(ex.ToString());
            throw;
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

    private static string BuildHistoryUrl(string basePath, MarketHistoryQueryOptions query, bool includePaging)
    {
        var qs = query.ToQueryString(includePaging);
        return string.IsNullOrEmpty(qs) ? basePath : $"{basePath}?{qs}";
    }

    private sealed record ApiMessageResponse(string? Message);
}
