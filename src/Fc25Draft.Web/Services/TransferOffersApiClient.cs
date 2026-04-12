using System.Net;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Web.Services;

public class TransferOffersApiClient
{
    private readonly ApiClientFactory _clientFactory;

    public TransferOffersApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<TransferOfferListItemDto> CreateOfferAsync(CreateTransferOfferDto dto, string teamToken, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/offers");
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());
        request.Content = JsonContent.Create(dto);

        using var response = await client.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<TransferOfferListItemDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Resposta inválida do servidor.");
    }

    public async Task<TransferOfferListItemDto> RespondToOfferAsync(Guid offerId, OfferStatus status, string teamToken, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/offers/{offerId}/respond");
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());
        request.Content = JsonContent.Create(new RespondToOfferDto(status));

        using var response = await client.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<TransferOfferListItemDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Resposta inválida do servidor.");
    }

    public async Task<TransferOfferListItemDto> CancelOfferAsync(Guid offerId, string teamToken, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/offers/{offerId}/cancel");
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());

        using var response = await client.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<TransferOfferListItemDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Resposta inválida do servidor.");
    }

    public async Task<IReadOnlyList<TransferOfferListItemDto>> GetReceivedAsync(string teamToken, CancellationToken ct = default)
    {
        return await GetWithTokenAsync<IReadOnlyList<TransferOfferListItemDto>>("api/offers/received", teamToken, ct)
            ?? Array.Empty<TransferOfferListItemDto>();
    }

    public async Task<IReadOnlyList<TransferOfferListItemDto>> GetSentAsync(string teamToken, CancellationToken ct = default)
    {
        return await GetWithTokenAsync<IReadOnlyList<TransferOfferListItemDto>>("api/offers/sent", teamToken, ct)
            ?? Array.Empty<TransferOfferListItemDto>();
    }

    public async Task<IReadOnlyList<TransferOfferListItemDto>> GetFinishedAsync(string teamToken, CancellationToken ct = default)
    {
        return await GetWithTokenAsync<IReadOnlyList<TransferOfferListItemDto>>("api/offers/finished", teamToken, ct)
            ?? Array.Empty<TransferOfferListItemDto>();
    }

    public async Task<TransferOfferListItemDto?> GetByIdAsync(Guid offerId, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        var response = await client.GetAsync($"api/offers/{offerId}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadFromJsonAsync<TransferOfferListItemDto>(cancellationToken: ct);
    }

    private async Task<T?> GetWithTokenAsync<T>(string url, string teamToken, CancellationToken ct)
    {
        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());

        using var response = await client.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        string? message = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: ct);
            message = error?.Message;
        }
        catch { }

        message ??= $"Erro ao comunicar com o servidor ({response.StatusCode}).";

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new UnauthorizedAccessException(message),
            HttpStatusCode.Forbidden => new UnauthorizedAccessException(message),
            HttpStatusCode.NotFound => new KeyNotFoundException(message),
            HttpStatusCode.Conflict => new InvalidOperationException(message),
            _ => new InvalidOperationException(message)
        };
    }

    private sealed record ErrorResponse(string? Message);
}
