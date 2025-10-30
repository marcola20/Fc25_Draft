using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fc25Draft.Core.DTOs;
using BidRequest = Fc25Draft.Core.DTOs.BidRequest;
using BuyNowRequest = Fc25Draft.Core.DTOs.BuyNowRequest;
using MarketItemVm = Fc25Draft.Core.DTOs.MarketItemVm;
using MarketQueryVm = Fc25Draft.Core.DTOs.MarketQueryVm;

namespace Fc25Draft.Web.Services
{
    public class MarketClient
    {
        private readonly HttpClient _http;

        public MarketClient(HttpClient http)
        {
            _http = http;
        }

        public async Task<PagedResult<MarketItemVm>> GetItemsAsync(MarketQueryVm query, CancellationToken ct)
        {
            // Build query string manually, handling nulls
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(query.Name)) qs.Add($"name={Uri.EscapeDataString(query.Name)}");
            if (query.Positions?.Any() == true) qs.Add($"positions={string.Join(",", query.Positions)}");
            if (query.OverallMin.HasValue) qs.Add($"overallMin={query.OverallMin.Value}");
            if (query.OverallMax.HasValue) qs.Add($"overallMax={query.OverallMax.Value}");
            if (!string.IsNullOrWhiteSpace(query.Status)) qs.Add($"status={query.Status}");
            qs.Add($"page={query.Page}");
            qs.Add($"pageSize={query.PageSize}");
            if (!string.IsNullOrWhiteSpace(query.Sort)) qs.Add($"sort={query.Sort}");

            var url = "/api/market/items";
            if (qs.Count > 0)
                url += "?" + string.Join("&", qs);

            using var resp = await _http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            // Optionally read server UTC time header
            if (resp.Headers.TryGetValues("x-server-time-utc", out var values))
            {
                var serverUtc = DateTime.Parse(values.First(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                LastServerTimeUtc = serverUtc;
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<PagedResult<MarketItemVm>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }

        public DateTime? LastServerTimeUtc { get; private set; }

        public async Task<MarketItemVm> PlaceBidAsync(BidRequest req, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/market/items/{req.ItemId}/bid");
            request.Headers.TryAddWithoutValidation("X-RowVersion", req.RowVersion);
            request.Content = JsonContent.Create(req);
            using var resp = await _http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.Conflict || resp.StatusCode == HttpStatusCode.PreconditionFailed)
                throw new InvalidOperationException("Outbid or stale version.");

            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<MarketItemVm>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Failed to deserialize MarketItemVm");
        }

        public async Task<MarketItemVm> BuyNowAsync(BuyNowRequest req, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/market/items/{req.ItemId}/buy-now");
            request.Headers.TryAddWithoutValidation("X-RowVersion", req.RowVersion);
            request.Content = JsonContent.Create(req);
            using var resp = await _http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.Conflict || resp.StatusCode == HttpStatusCode.PreconditionFailed)
                throw new InvalidOperationException("Outbid or stale version.");

            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<MarketItemVm>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Failed to deserialize MarketItemVm");
        }
    }
}
