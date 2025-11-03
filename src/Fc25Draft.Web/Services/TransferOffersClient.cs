using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fc25Draft.Core.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace Fc25Draft.Web.Services;

public class TransferOffersClient
{
    private const string InvalidTeamTokenMessage = "Token do time inválido ou expirado. Informe novamente.";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private readonly ApiClientFactory _clientFactory;
    private readonly TeamAccessService _teamAccess;
    private readonly ToastService _toasts;
    private readonly ILogger<TransferOffersClient> _logger;

    public TransferOffersClient(
        ApiClientFactory clientFactory,
        TeamAccessService teamAccess,
        ToastService toasts,
        ILogger<TransferOffersClient> logger)
    {
        _clientFactory = clientFactory;
        _teamAccess = teamAccess;
        _toasts = toasts;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TransferOfferSummaryDto>> GetReceivedAsync(CancellationToken ct)
        => await GetOfferSummariesAsync("/api/transfers/offers/received", ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<TransferOfferSummaryDto>> GetSentAsync(CancellationToken ct)
        => await GetOfferSummariesAsync("/api/transfers/offers/sent", ct).ConfigureAwait(false);

    public async Task<TransferOfferDetailDto?> GetByIdAsync(Guid offerId, CancellationToken ct)
    {
        if (offerId == Guid.Empty)
        {
            return null;
        }

        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/transfers/offers/{offerId:D}");
        if (!await TryAttachTeamTokenAsync(request).ConfigureAwait(false))
        {
            return null;
        }

        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await HandleInvalidTokenAsync().ConfigureAwait(false);
            throw new TeamTokenMissingException();
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response).ConfigureAwait(false);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "Não foi possível carregar os detalhes da proposta."
                : message);
        }

        return await response.Content.ReadFromJsonAsync<TransferOfferDetailDto>(SerializerOptions, ct).ConfigureAwait(false);
    }

    public async Task<TransferOfferActionResult> CreateAsync(CreateTransferOfferClientRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/transfers/offers")
        {
            Content = JsonContent.Create(new
            {
                toTeamId = request.ToTeamId,
                playerId = request.PlayerId,
                offeredFee = request.OfferedFee,
                sellOnFeePercentage = request.SellOnFeePercentage,
                swapPlayerIds = request.SwapPlayerIds ?? Array.Empty<int>(),
                message = SanitizeOptional(request.Message),
                expiresAtUtc = request.ExpiresAtUtc
            })
        };

        if (!await TryAttachTeamTokenAsync(message).ConfigureAwait(false))
        {
            throw new TeamTokenMissingException();
        }

        using var response = await client.SendAsync(message, ct).ConfigureAwait(false);
        return await HandleDetailResponseAsync(response, "Proposta enviada com sucesso.", ct).ConfigureAwait(false);
    }

    public async Task<TransferOfferActionResult> AcceptAsync(Guid offerId, uint rowVersion, CancellationToken ct)
    {
        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/transfers/offers/{offerId:D}/accept");
        ApplyRowVersionHeaders(message, rowVersion);

        if (!await TryAttachTeamTokenAsync(message).ConfigureAwait(false))
        {
            throw new TeamTokenMissingException();
        }

        using var response = await client.SendAsync(message, ct).ConfigureAwait(false);
        return await HandleDetailResponseAsync(response, "Oferta aceita com sucesso.", ct).ConfigureAwait(false);
    }

    public async Task<TransferOfferActionResult> RejectAsync(Guid offerId, uint rowVersion, string? responseMessage, CancellationToken ct)
    {
        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/transfers/offers/{offerId:D}/reject")
        {
            Content = JsonContent.Create(new { responseMessage = SanitizeOptional(responseMessage) })
        };
        ApplyRowVersionHeaders(message, rowVersion);

        if (!await TryAttachTeamTokenAsync(message).ConfigureAwait(false))
        {
            throw new TeamTokenMissingException();
        }

        using var response = await client.SendAsync(message, ct).ConfigureAwait(false);
        return await HandleDetailResponseAsync(response, "Oferta rejeitada.", ct).ConfigureAwait(false);
    }

    public async Task<TransferOfferActionResult> WithdrawAsync(Guid offerId, uint rowVersion, CancellationToken ct)
    {
        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/transfers/offers/{offerId:D}/withdraw");
        ApplyRowVersionHeaders(message, rowVersion);

        if (!await TryAttachTeamTokenAsync(message).ConfigureAwait(false))
        {
            throw new TeamTokenMissingException();
        }

        using var response = await client.SendAsync(message, ct).ConfigureAwait(false);
        return await HandleDetailResponseAsync(response, "Oferta cancelada.", ct).ConfigureAwait(false);
    }

    public async Task<TransferOfferActionResult> CounterAsync(Guid offerId, uint rowVersion, CounterTransferOfferClientRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/transfers/offers/{offerId:D}/counter")
        {
            Content = JsonContent.Create(new
            {
                offeredFee = request.OfferedFee,
                sellOnFeePercentage = request.SellOnFeePercentage,
                swapPlayerIds = request.SwapPlayerIds ?? Array.Empty<int>(),
                message = SanitizeOptional(request.Message),
                expiresAtUtc = request.ExpiresAtUtc
            })
        };
        ApplyRowVersionHeaders(message, rowVersion);

        if (!await TryAttachTeamTokenAsync(message).ConfigureAwait(false))
        {
            throw new TeamTokenMissingException();
        }

        using var response = await client.SendAsync(message, ct).ConfigureAwait(false);
        return await HandleDetailResponseAsync(response, "Contraproposta enviada com sucesso.", ct).ConfigureAwait(false);
    }

    public async Task<TeamIdentityDto?> GetMyTeamAsync(CancellationToken ct)
    {
        var token = await GetTokenOrDefaultAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/teams/me");
        request.Headers.TryAddWithoutValidation("X-Team-Token", token);

        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await HandleInvalidTokenAsync().ConfigureAwait(false);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response).ConfigureAwait(false);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "Não foi possível carregar os dados do time."
                : message);
        }

        return await response.Content.ReadFromJsonAsync<TeamIdentityDto>(SerializerOptions, ct).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<TransferOfferSummaryDto>> GetOfferSummariesAsync(string url, CancellationToken ct)
    {
        var client = await _clientFactory.CreateAsync().ConfigureAwait(false);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!await TryAttachTeamTokenAsync(request).ConfigureAwait(false))
        {
            throw new TeamTokenMissingException();
        }

        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await HandleInvalidTokenAsync().ConfigureAwait(false);
            throw new TeamTokenMissingException();
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response).ConfigureAwait(false);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "Não foi possível carregar as propostas."
                : message);
        }

        var summaries = await response.Content.ReadFromJsonAsync<IReadOnlyList<TransferOfferSummaryDto>>(SerializerOptions, ct)
            .ConfigureAwait(false);
        return summaries ?? Array.Empty<TransferOfferSummaryDto>();
    }

    private async Task<bool> TryAttachTeamTokenAsync(HttpRequestMessage request)
    {
        var token = await GetTokenOrDefaultAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        request.Headers.TryAddWithoutValidation("X-Team-Token", token);
        return true;
    }

    private async Task<string?> GetTokenOrDefaultAsync()
    {
        try
        {
            return await _teamAccess.GetTokenAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (IsPrerenderInteropException(ex))
        {
            _logger.LogDebug(ex, "Team token unavailable before first render.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve stored team token.");
            return null;
        }
    }

    private async Task<TransferOfferActionResult> HandleDetailResponseAsync(HttpResponseMessage response, string successMessage, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            await HandleInvalidTokenAsync().ConfigureAwait(false);
            throw new TeamTokenMissingException();
        }

        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.PreconditionFailed)
        {
            var conflict = await ReadErrorMessageAsync(response).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(conflict))
            {
                conflict = "A proposta foi atualizada. Atualize os dados e tente novamente.";
            }

            throw new InvalidOperationException(conflict);
        }

        if (!response.IsSuccessStatusCode)
        {
            var message = await ReadErrorMessageAsync(response).ConfigureAwait(false);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(message)
                ? "Não foi possível processar a solicitação."
                : message);
        }

        var detail = await response.Content.ReadFromJsonAsync<TransferOfferDetailDto>(SerializerOptions, ct).ConfigureAwait(false);
        if (detail is null)
        {
            throw new InvalidOperationException("Resposta inválida do servidor.");
        }

        return new TransferOfferActionResult(detail, successMessage);
    }

    private async Task HandleInvalidTokenAsync()
    {
        _logger.LogWarning("Team token rejected by server. Clearing stored token.");
        _teamAccess.ReportInvalidToken(InvalidTeamTokenMessage);

        try
        {
            await _teamAccess.ClearTokenAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear team token after rejection.");
        }

        _toasts.ShowError(InvalidTeamTokenMessage);
    }

    private static void ApplyRowVersionHeaders(HttpRequestMessage request, uint rowVersion)
    {
        var value = rowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        request.Headers.TryAddWithoutValidation("X-RowVersion", value);
        request.Headers.TryAddWithoutValidation(HeaderNames.IfMatch, $"W/\"{value}\"");
    }

    private static string? SanitizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                if (document.RootElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String)
                {
                    return messageElement.GetString() ?? string.Empty;
                }

                if (document.RootElement.TryGetProperty("detail", out var detailElement) && detailElement.ValueKind == JsonValueKind.String)
                {
                    return detailElement.GetString() ?? string.Empty;
                }

                if (document.RootElement.TryGetProperty("error", out var errorElement) && errorElement.ValueKind == JsonValueKind.String)
                {
                    return errorElement.GetString() ?? string.Empty;
                }

                if (document.RootElement.TryGetProperty("title", out var titleElement) && titleElement.ValueKind == JsonValueKind.String)
                {
                    return titleElement.GetString() ?? string.Empty;
                }
            }
        }
        catch (JsonException)
        {
            // Ignore parsing issues and fallback to plain text body.
        }

        var trimmed = body.Trim();
        return trimmed.Length > 0 ? trimmed : string.Empty;
    }

    private static bool IsPrerenderInteropException(InvalidOperationException ex)
        => ex.Message.Contains("JavaScript interop calls cannot be issued", StringComparison.Ordinal)
            || ex.Message.Contains("the component is prerendering", StringComparison.Ordinal);
}

public sealed record CreateTransferOfferClientRequest(
    Guid ToTeamId,
    int PlayerId,
    decimal? OfferedFee,
    decimal? SellOnFeePercentage,
    IReadOnlyCollection<int>? SwapPlayerIds,
    string? Message,
    DateTime? ExpiresAtUtc);

public sealed record CounterTransferOfferClientRequest(
    decimal? OfferedFee,
    decimal? SellOnFeePercentage,
    IReadOnlyCollection<int>? SwapPlayerIds,
    string? Message,
    DateTime? ExpiresAtUtc);

public sealed record TransferOfferActionResult(TransferOfferDetailDto Offer, string Message);
