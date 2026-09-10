using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Web.Services;

public class DraftWishlistApiClient
{
    private const string Route = "api/draft/wishlist";
    private const string AdminRoute = "api/admin/draft/wishlist";
    private readonly ApiClientFactory _clientFactory;

    public DraftWishlistApiClient(ApiClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IReadOnlyList<DraftWishlistEdicaoDto>> GetEdicoesAsync(CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var response = await client.GetAsync($"{Route}/edicoes", ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<DraftWishlistEdicaoDto>>(cancellationToken: ct);
        return result ?? Array.Empty<DraftWishlistEdicaoDto>();
    }

    public async Task<DraftWishlistDto> GetMineAsync(string teamToken, int? versao = null, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, WithVersao(Route, versao));
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

    public async Task<IReadOnlyList<DraftWishlistDto>> GetAllAsync(int? versao = null, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.GetAsync(WithVersao(AdminRoute, versao), ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<DraftWishlistDto>>(cancellationToken: ct);
        return result ?? Array.Empty<DraftWishlistDto>();
    }

    public async Task<IReadOnlyList<DraftWishlistVoteDto>> GetVotesAsync(int? versao = null, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.GetAsync(WithVersao($"{AdminRoute}/votes", versao), ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<DraftWishlistVoteDto>>(cancellationToken: ct);
        return result ?? Array.Empty<DraftWishlistVoteDto>();
    }

    public async Task<DraftWishlistEdicaoDto> AbrirNovaEdicaoAsync(string? nome, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PostAsJsonAsync($"{AdminRoute}/edicoes", new DraftWishlistNovaEdicaoRequestDto(nome), ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<DraftWishlistEdicaoDto>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("Resposta inválida do servidor.");
    }

    public async Task<DraftWishlistEdicaoDto> AlterarStatusEdicaoAsync(int numero, bool aberta, CancellationToken ct = default)
    {
        var client = await _clientFactory.CreateAsync(includeAdminToken: true);
        using var response = await client.PutAsJsonAsync(
            $"{AdminRoute}/edicoes/{numero}/status",
            new DraftWishlistEdicaoStatusRequestDto(aberta),
            ct);
        await EnsureSuccessAsync(response, ct);

        var result = await response.Content.ReadFromJsonAsync<DraftWishlistEdicaoDto>(cancellationToken: ct);
        return result ?? throw new InvalidOperationException("Resposta inválida do servidor.");
    }

    private static string WithVersao(string route, int? versao) =>
        versao.HasValue ? $"{route}?versao={versao.Value}" : route;

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

        if (message is null && response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            message = "Ação permitida somente para administradores. Verifique o token informado.";

        throw new InvalidOperationException(message ?? $"Erro ao comunicar com o servidor ({(int)response.StatusCode}).");
    }

    private sealed record ApiErrorResponse(string? Message);
}
