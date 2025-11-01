using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Web.Models.Market;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using BidRequest = Fc25Draft.Core.DTOs.BidRequest;
using BuyNowRequest = Fc25Draft.Core.DTOs.BuyNowRequest;
using CoreItemVm = Fc25Draft.Core.DTOs.MarketItemVm;
using CoreQueryVm = Fc25Draft.Core.DTOs.MarketQueryVm;

namespace Fc25Draft.Web.Services
{
    public class MarketClient
    {
        private const string InvalidTeamTokenMessage = "Token do time inválido ou expirado. Informe novamente.";

        private readonly ApiClientFactory _clientFactory;
        private readonly TeamAccessService _teamAccess;
        private readonly ToastService _toasts;
        private readonly ILogger<MarketClient> _logger;

        public MarketClient(ApiClientFactory clientFactory, TeamAccessService teamAccess, ToastService toasts, ILogger<MarketClient> logger)
        {
            _clientFactory = clientFactory;
            _teamAccess = teamAccess;
            _toasts = toasts;
            _logger = logger;
        }

        public async Task<PagedResult<CoreItemVm>> GetItemsAsync(Guid? cycleId, CoreQueryVm query, CancellationToken ct)
        {
            var qs = new List<string>();
            if (cycleId.HasValue && cycleId.Value != Guid.Empty)
            {
                qs.Add($"cycleId={cycleId.Value:D}");
            }

            if (!string.IsNullOrWhiteSpace(query.Name)) qs.Add($"q={Uri.EscapeDataString(query.Name)}");
            if (query.Positions?.Any() == true)
            {
                var positionsParam = string.Join(',', query.Positions.Distinct());
                qs.Add($"positions={positionsParam}");
            }
            if (query.OverallMin.HasValue) qs.Add($"overallMin={query.OverallMin.Value}");
            if (query.OverallMax.HasValue) qs.Add($"overallMax={query.OverallMax.Value}");
            if (!string.IsNullOrWhiteSpace(query.Status)) qs.Add($"status={Uri.EscapeDataString(query.Status)}");
            qs.Add($"page={Math.Max(1, query.Page)}");
            qs.Add($"pageSize={Math.Max(1, query.PageSize)}");
            if (TryResolveSort(query.Sort) is { } sort)
            {
                qs.Add($"sortBy={Uri.EscapeDataString(sort.SortBy)}");
                qs.Add($"sortOrder={sort.SortOrder}");
            }

            var url = "/api/market/items";
            if (qs.Count > 0) url += "?" + string.Join("&", qs);

            var http = await _clientFactory.CreateAsync();
            using var resp = await http.GetAsync(url, ct);

            var body = await resp.Content.ReadAsStringAsync(ct);
            if (resp.StatusCode == HttpStatusCode.Conflict)
            {
                var message = await ReadErrorMessageAsync(resp);
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = "Este ciclo ainda não está ativo.";
                }
                throw new MarketCycleUnavailableException(message, resp.StatusCode);
            }

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"GET {url} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

            if (resp.Headers.TryGetValues("x-server-time-utc", out var values) &&
                DateTime.TryParse(values.FirstOrDefault(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var serverUtc))
            {
                LastServerTimeUtc = serverUtc;
            }

            var json = body ?? string.Empty;
            var trimmed = json.AsSpan().Trim();

            if (trimmed.Length == 0 || trimmed.SequenceEqual("null".AsSpan()))
                return PagedResult<CoreItemVm>.Empty(query.Page, query.PageSize);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };
            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: true));

            try
            {
                var dtoResult = JsonSerializer.Deserialize<PagedResult<MarketItemListDto>>(json, options);

                return dtoResult is null
                    ? PagedResult<CoreItemVm>.Empty(query.Page, query.PageSize)
                    : MapToViewModel(dtoResult);
            }
            catch (JsonException jex)
            {
                var contentType = resp.Content.Headers.ContentType?.MediaType ?? "(desconhecido)";
                Console.WriteLine($"⚠️ JSON inválido para {nameof(PagedResult<CoreItemVm>)}: {jex.Message}");
                Console.WriteLine($"content-type={contentType}");
                var preview = json.Length > 500 ? json.Substring(0, 500) : json;
                Console.WriteLine($"payload (até 500 chars): {preview}");
                return PagedResult<CoreItemVm>.Empty(query.Page, query.PageSize);
            }
        }

        public DateTime? LastServerTimeUtc { get; private set; }

        public async Task<MarketClientActionResult<CoreItemVm>> PlaceBidAsync(BidRequest req, CancellationToken ct)
        {
            var http = await _clientFactory.CreateAsync();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/market/items/{req.ItemId}/bids");
            ApplyRowVersionHeaders(request, req.RowVersion);
            await AttachTeamTokenAsync(request);
            request.Content = JsonContent.Create(new { amount = req.Amount });
            using var resp = await http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                await HandleInvalidTokenAsync(InvalidTeamTokenMessage);
                throw new TeamTokenMissingException();
            }

            if (resp.StatusCode == HttpStatusCode.Conflict || resp.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                var errorMessage = await ReadErrorMessageAsync(resp);
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = "Não foi possível registrar o lance porque o item foi atualizado. Atualize os dados e tente novamente.";
                }
                throw new MarketConcurrencyException(errorMessage, resp.StatusCode);
            }

            if (!resp.IsSuccessStatusCode)
            {
                var message = await ReadErrorMessageAsync(resp);
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = $"Falha ao registrar o lance. Código {(int)resp.StatusCode}.";
                }
                throw new MarketClientException(message, resp.StatusCode);
            }

            var dto = await resp.Content.ReadFromJsonAsync<MarketItemDto>(
                options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken: ct);

            if (dto is null)
            {
                throw new InvalidOperationException("Resposta inválida ao registrar o lance.");
            }

            var viewModel = MapToViewModel(dto);
            return new MarketClientActionResult<CoreItemVm>(viewModel, "Lance registrado com sucesso.");
        }

        public async Task<MarketClientActionResult<CoreItemVm>> BuyNowAsync(BuyNowRequest req, CancellationToken ct)
        {
            var http = await _clientFactory.CreateAsync();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/market/{req.ItemId}/buy-now");
            ApplyRowVersionHeaders(request, req.RowVersion);
            await AttachTeamTokenAsync(request);
            request.Content = JsonContent.Create(req);
            using var resp = await http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                await HandleInvalidTokenAsync(InvalidTeamTokenMessage);
                throw new TeamTokenMissingException();
            }

            if (resp.StatusCode == HttpStatusCode.Conflict || resp.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                var errorMessage = await ReadErrorMessageAsync(resp);
                if (string.IsNullOrWhiteSpace(errorMessage))
                {
                    errorMessage = "Não foi possível concluir a compra porque o item foi atualizado. Atualize os dados e tente novamente.";
                }
                throw new MarketConcurrencyException(errorMessage, resp.StatusCode);
            }

            if (!resp.IsSuccessStatusCode)
            {
                var message2 = await ReadErrorMessageAsync(resp);
                if (string.IsNullOrWhiteSpace(message2))
                {
                    message2 = $"Falha ao concluir a compra. Código {(int)resp.StatusCode}.";
                }
                throw new MarketClientException(message2, resp.StatusCode);
            }

            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<BuyNowResultDto>(cancellationToken: ct);
            var message = result?.Message ?? "Compra realizada com sucesso.";

            var updatedItem = await FetchItemAsync(http, req.ItemId, ct)
                ?? throw new InvalidOperationException("Failed to reload market item after buy now operation.");

            return new MarketClientActionResult<CoreItemVm>(updatedItem, message);
        }

        public async Task<TeamIdentityDto?> GetMyTeamAsync(string? tokenOverride = null, CancellationToken ct = default)
        {
            string? token;

            if (string.IsNullOrWhiteSpace(tokenOverride))
            {
                token = await _teamAccess.GetTokenAsync();
            }
            else
            {
                token = tokenOverride.Trim();
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var http = await _clientFactory.CreateAsync();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/teams/me");
            request.Headers.TryAddWithoutValidation("X-Team-Token", token);

            using var resp = await http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                await HandleInvalidTokenAsync(InvalidTeamTokenMessage);
                return null;
            }

            if (!resp.IsSuccessStatusCode)
            {
                var message = await ReadErrorMessageAsync(resp);
                if (string.IsNullOrWhiteSpace(message))
                {
                    message = $"Falha ao carregar dados do time. Código {(int)resp.StatusCode}.";
                }
                throw new MarketClientException(message, resp.StatusCode);
            }

            var identity = await resp.Content.ReadFromJsonAsync<TeamIdentityDto>(
                options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken: ct);

            return identity;
        }

        public async Task<PagedResult<MarketTransactionDto>> GetHistoryAsync(MarketHistoryQueryOptions query, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(query);

            var url = BuildHistoryUrl("/api/market/history", query, includePaging: true);
            var http = await _clientFactory.CreateAsync();
            using var resp = await http.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"GET {url} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
            }

            resp.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<PagedResult<MarketTransactionDto>>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return result ?? PagedResult<MarketTransactionDto>.Empty(query.Page, query.PageSize);
        }

        public string GetHistoryExportUrl(MarketHistoryQueryOptions query)
        {
            ArgumentNullException.ThrowIfNull(query);

            var basePath = "/api/market/history/export";
            var qs = query.ToQueryString(includePaging: false);
            return string.IsNullOrEmpty(qs) ? basePath : $"{basePath}?{qs}";
        }

        private async Task AttachTeamTokenAsync(HttpRequestMessage request)
        {
            var token = await _teamAccess.GetTokenAsync();
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new TeamTokenMissingException();
            }

            request.Headers.TryAddWithoutValidation("X-Team-Token", token);
        }

        private async Task HandleInvalidTokenAsync(string message)
        {
            _logger.LogWarning("Team token rejected by the server. Clearing local token.");
            _teamAccess.ReportInvalidToken(message);

            try
            {
                await _teamAccess.ClearTokenAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clear team token after invalidation.");
            }

            _toasts.ShowError(message);
        }

        private static void ApplyRowVersionHeaders(HttpRequestMessage request, string? rowVersion)
        {
            if (string.IsNullOrWhiteSpace(rowVersion))
            {
                return;
            }

            var sanitized = rowVersion.Trim();
            request.Headers.TryAddWithoutValidation("X-RowVersion", sanitized);
            request.Headers.TryAddWithoutValidation(HeaderNames.IfMatch, $"W/\"{sanitized}\"");
        }

        private static (string SortBy, string SortOrder)? TryResolveSort(string? rawSort)
        {
            if (string.IsNullOrWhiteSpace(rawSort))
            {
                return null;
            }

            var trimmed = rawSort.Trim();
            var descending = trimmed.StartsWith("-", StringComparison.Ordinal); 
            var token = descending ? trimmed[1..] : trimmed;

            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var normalized = token.ToLowerInvariant();
            var sortBy = normalized switch
            {
                "expiresatutc" or "expires_at_utc" or "expiresat" => "expiresAtUtc",
                "currentbid" or "current_bid" => "currentBid",
                _ => null
            };

            if (sortBy is null)
            {
                return null;
            }

            var sortOrder = descending ? "desc" : "asc";
            return (sortBy, sortOrder);
        }

        private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage resp)
        {
            var body = await resp.Content.ReadAsStringAsync();

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                        {
                            return m.GetString()!;
                        }

                        if (doc.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String)
                        {
                            return e.GetString()!;
                        }

                        if (doc.RootElement.TryGetProperty("detail", out var d) && d.ValueKind == JsonValueKind.String)
                        {
                            return d.GetString()!;
                        }

                        if (doc.RootElement.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String)
                        {
                            return t.GetString()!;
                        }
                    }
                }
                catch (JsonException)
                {
                    // Ignore parse errors and fall back to the raw body.
                }

                var trimmed = body.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    return trimmed;
                }
            }

            return $"{(int)resp.StatusCode} {resp.ReasonPhrase}";
        }

        private static PagedResult<CoreItemVm> MapToViewModel(PagedResult<MarketItemListDto> source)
        {
            var items = source.Items?.Select(MapToViewModel).ToList() ?? new List<CoreItemVm>();
            return new PagedResult<CoreItemVm>(items, source.Total, source.Page, source.PageSize);
        }

        private static CoreItemVm MapToViewModel(MarketItemListDto dto)
        {
            return new CoreItemVm
            {
                ItemId = dto.ItemId,
                PlayerId = dto.PlayerId,
                PlayerName = dto.PlayerName,
                PositionId = dto.Position.ToPositionId(),
                Overall = dto.Overall,
                BasePrice = dto.BasePrice,
                CurrentLeaderTeamId = dto.CurrentLeaderTeamId,
                CurrentLeaderTeamName = dto.CurrentLeaderTeamName,
                CurrentLeaderAmount = dto.CurrentBid,
                BuyNowPrice = dto.BuyNowPrice,
                MinIncrement = dto.MinIncrement,
                RequiredMinBid = dto.RequiredMinBid,
                ExpiresAtUtc = dto.ExpiresAtUtc,
                Status = dto.StatusText,
                RowVersion = dto.RowVersion.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static CoreItemVm MapToViewModel(MarketItemDto dto)
        {
            return new CoreItemVm
            {
                ItemId = dto.ItemId,
                PlayerId = dto.PlayerId,
                PlayerName = dto.PlayerName,
                PositionId = dto.Position.ToPositionId(),
                Overall = dto.Ovr,
                BasePrice = dto.BasePrice,
                CurrentLeaderTeamId = dto.CurrentLeaderTeamId,
                CurrentLeaderTeamName = dto.CurrentLeaderTeamName,
                CurrentLeaderAmount = dto.CurrentLeaderAmount,
                BuyNowPrice = dto.BuyNowPrice,
                MinIncrement = dto.MinIncrement,
                RequiredMinBid = dto.RequiredMinBid,
                ExpiresAtUtc = dto.ExpiresAtUtc,
                Status = dto.Status,
                RowVersion = dto.RowVersion.ToString(CultureInfo.InvariantCulture)
            };
        }

        private static async Task<CoreItemVm?> FetchItemAsync(HttpClient http, Guid itemId, CancellationToken ct)
        {
            using var resp = await http.GetAsync($"/api/market/{itemId}", ct);
            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            resp.EnsureSuccessStatusCode();

            var dto = await resp.Content.ReadFromJsonAsync<MarketItemDto>(
                options: new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                cancellationToken: ct);

            return dto is null ? null : MapToViewModel(dto);
        }

        private static string BuildHistoryUrl(string basePath, MarketHistoryQueryOptions query, bool includePaging)
        {
            var qs = query.ToQueryString(includePaging);
            return string.IsNullOrEmpty(qs) ? basePath : $"{basePath}?{qs}";
        }
    }

    public sealed record MarketClientActionResult<T>(T Payload, string Message);
}
