using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Web.Models.MarketCycles;

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
        var result = await JsonSerializer.DeserializeAsync<PagedResult<MarketCycleDto>>(stream, SerializerOptions, ct).ConfigureAwait(false);

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
        using var response = await client.PostAsJsonAsync("api/admin/market/cycles", request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<MarketCycleDto>(SerializerOptions, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Resposta inválida do servidor ao criar o ciclo de mercado.");
    }

    public async Task<MarketCycleDto> UpdateStatusAsync(Guid cycleId, MarketCycleStatus status, bool forceClose, CancellationToken ct = default)
    {
        var request = new MarketCycleStatusUpdateRequest
        {
            Status = status,
            ForceClose = forceClose
        };

        var client = await _clientFactory.CreateAsync(includeAdminToken: true).ConfigureAwait(false);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, $"api/admin/market/cycles/{cycleId}/status")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await client.SendAsync(httpRequest, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        return await response.Content.ReadFromJsonAsync<MarketCycleDto>(SerializerOptions, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Resposta inválida do servidor ao atualizar o status do ciclo.");
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
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var message = await ReadErrorMessageAsync(response, ct).ConfigureAwait(false);
        throw new InvalidOperationException(message);
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(SerializerOptions, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return error.Message;
            }

            if (error?.Errors is { Count: > 0 })
            {
                return string.Join(" ", error.Errors.SelectMany(pair => pair.Value ?? Array.Empty<string>()));
            }
        }
        catch
        {
            // Ignorado: fallback abaixo.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                => "Ação permitida somente para administradores. Verifique o token informado.",
            HttpStatusCode.NotFound
                => "Ciclo do mercado não encontrado.",
            HttpStatusCode.Conflict
                => "A operação não pôde ser concluída devido a um conflito no estado atual do mercado.",
            _ => "Não foi possível comunicar com o servidor. Tente novamente."
        };
    }

    private sealed record ApiErrorResponse(string? Message, Dictionary<string, string[]?>? Errors);
}
