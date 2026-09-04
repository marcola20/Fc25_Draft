using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

public class DraftWishlistApiClient
{
    private const string Route = "api/draft/wishlist";
    private readonly ApiClientFactory _clientFactory;

    public DraftWishlistApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<DraftWishlistDto> GetMineAsync(string teamToken, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, Route);
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());

        using var response = await client.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<DraftWishlistDto>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("Resposta inválida do servidor.");
    }

    public async Task<DraftWishlistDto> SaveAsync(string teamToken, IReadOnlyList<int> playerIds, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Put, Route)
        {
            Content = JsonContent.Create(new DraftWishlistSaveRequestDto(playerIds))
        };
        request.Headers.TryAddWithoutValidation("X-Team-Token", teamToken.Trim());

        using var response = await client.SendAsync(request, ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<DraftWishlistDto>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("Resposta inválida do servidor.");
    }

    public async Task<IReadOnlyList<DraftWishlistDto>> GetAllAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.GetAsync("api/admin/draft/wishlist", ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<DraftWishlistDto>>(cancellationToken: ct);
        return result ?? Array.Empty<DraftWishlistDto>();
    }

    public async Task<IReadOnlyList<DraftWishlistVoteDto>> GetVotesAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.GetAsync("api/admin/draft/wishlist/votes", ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<DraftWishlistVoteDto>>(cancellationToken: ct);
        return result ?? Array.Empty<DraftWishlistVoteDto>();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        string? message = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(cancellationToken: ct);
            message = error?.Message;
        }
        catch
        {
            // corpo não é JSON
        }

        throw new InvalidOperationException(message ?? $"Erro ao comunicar com o servidor ({(int)response.StatusCode}).");
    }

    private sealed record ApiErrorResponse(string? Message);
}
