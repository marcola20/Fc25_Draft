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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using BidRequest = Fc25Draft.Core.DTOs.BidRequest;
using BuyNowRequest = Fc25Draft.Core.DTOs.BuyNowRequest;
using CoreItemVm = Fc25Draft.Core.DTOs.MarketItemVm;
using CoreQueryVm = Fc25Draft.Core.DTOs.MarketQueryVm;

namespace Fc25Draft.Web.Services
{
    public class MarketClient
    {
        private readonly ApiClientFactory _clientFactory;

        public MarketClient(ApiClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public async Task<PagedResult<CoreItemVm>> GetItemsAsync(CoreQueryVm query, CancellationToken ct)
        {
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(query.Name)) qs.Add($"name={Uri.EscapeDataString(query.Name)}");
            if (query.Positions?.Any() == true) qs.Add($"positions={string.Join(",", query.Positions)}");
            if (query.OverallMin.HasValue) qs.Add($"overallMin={query.OverallMin.Value}");
            if (query.OverallMax.HasValue) qs.Add($"overallMax={query.OverallMax.Value}");
            if (!string.IsNullOrWhiteSpace(query.Status)) qs.Add($"status={Uri.EscapeDataString(query.Status)}");
            qs.Add($"page={query.Page}");
            qs.Add($"pageSize={query.PageSize}");
            if (!string.IsNullOrWhiteSpace(query.Sort)) qs.Add($"sort={Uri.EscapeDataString(query.Sort)}");

            var url = "/api/market";
            if (qs.Count > 0)
                url += "?" + string.Join("&", qs);

            var http = await _clientFactory.CreateAsync();
            using var resp = await http.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"GET {url} -> {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");

            resp.EnsureSuccessStatusCode();

            if (resp.Headers.TryGetValues("x-server-time-utc", out var values))
            {
                var serverUtc = DateTime.Parse(values.First(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                LastServerTimeUtc = serverUtc;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);

            if (string.IsNullOrWhiteSpace(json) || json == "null")
            {
                return PagedResult<CoreItemVm>.Empty(query.Page, query.PageSize);
            }

            try
            {
                var result = JsonSerializer.Deserialize<PagedResult<CoreItemVm>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return result ?? PagedResult<CoreItemVm>.Empty(query.Page, query.PageSize);
            }
            catch (JsonException jex)
            {
                Console.WriteLine($"⚠️ Falha ao desserializar JSON em {nameof(PagedResult<CoreItemVm>)}: {jex.Message}");
                Console.WriteLine($"Conteúdo recebido: '{json}'");
                return PagedResult<CoreItemVm>.Empty(query.Page, query.PageSize);
            }

        }

        public DateTime? LastServerTimeUtc { get; private set; }

        public async Task<MarketClientActionResult<CoreItemVm>> PlaceBidAsync(BidRequest req, CancellationToken ct)
        {
            var http = await _clientFactory.CreateAsync();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/market/{req.ItemId}/bid");
            ApplyRowVersionHeaders(request, req.RowVersion);
            request.Content = JsonContent.Create(req);
            using var resp = await http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.Conflict || resp.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                var errorMessage = await ExtractProblemMessageAsync(resp, ct)
                    ?? "Não foi possível registrar o lance porque o item foi atualizado. Atualize os dados e tente novamente.";
                throw new MarketConcurrencyException(errorMessage, resp.StatusCode);
            }

            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<BidResultDto>(cancellationToken: ct);
            var message = result?.Message ?? "Lance registrado com sucesso.";

            var updatedItem = await FetchItemAsync(http, req.ItemId, ct)
                ?? throw new InvalidOperationException("Failed to reload market item after bid.");

            return new MarketClientActionResult<CoreItemVm>(updatedItem, message);
        }

        public async Task<MarketClientActionResult<CoreItemVm>> BuyNowAsync(BuyNowRequest req, CancellationToken ct)
        {
            var http = await _clientFactory.CreateAsync();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/market/{req.ItemId}/buy-now");
            ApplyRowVersionHeaders(request, req.RowVersion);
            request.Content = JsonContent.Create(req);
            using var resp = await http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.Conflict || resp.StatusCode == HttpStatusCode.PreconditionFailed)
            {
                var errorMessage = await ExtractProblemMessageAsync(resp, ct)
                    ?? "Não foi possível concluir a compra porque o item foi atualizado. Atualize os dados e tente novamente.";
                throw new MarketConcurrencyException(errorMessage, resp.StatusCode);
            }

            resp.EnsureSuccessStatusCode();
            var result = await resp.Content.ReadFromJsonAsync<BuyNowResultDto>(cancellationToken: ct);
            var message = result?.Message ?? "Compra realizada com sucesso.";

            var updatedItem = await FetchItemAsync(http, req.ItemId, ct)
                ?? throw new InvalidOperationException("Failed to reload market item after buy now operation.");

            return new MarketClientActionResult<CoreItemVm>(updatedItem, message);
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

        private static async Task<string?> ExtractProblemMessageAsync(HttpResponseMessage response, CancellationToken ct)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                var problem = JsonSerializer.Deserialize<ProblemDetails>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return problem?.Detail ?? problem?.Title ?? body;
            }
            catch
            {
                return body;
            }
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
                CurrentLeaderTeamId = dto.CurrentLeaderTeamId,
                CurrentLeaderTeamName = dto.CurrentLeaderTeamName,
                CurrentLeaderAmount = dto.CurrentLeaderAmount,
                BuyNowPrice = dto.BuyNowPrice,
                MinIncrement = dto.MinIncrement,
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
