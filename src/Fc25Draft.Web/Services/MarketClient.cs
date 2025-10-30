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
            // Build query string manually, handling nulls
            var qs = new List<string>();
            if (!string.IsNullOrWhiteSpace(query.Name)) qs.Add($"name={Uri.EscapeDataString(query.Name)}");
            if (query.Positions?.Any() == true) qs.Add($"positions={string.Join(",", query.Positions)}");
            if (query.OverallMin.HasValue) qs.Add($"overallMin={query.OverallMin.Value}");
            if (query.OverallMax.HasValue) qs.Add($"overallMax={query.OverallMax.Value}");
            if (!string.IsNullOrWhiteSpace(query.Status)) qs.Add($"status={Uri.EscapeDataString(query.Status)}");
            qs.Add($"page={query.Page}");
            qs.Add($"pageSize={query.PageSize}");
            if (!string.IsNullOrWhiteSpace(query.Sort)) qs.Add($"sort={Uri.EscapeDataString(query.Sort)}");

            // Public market endpoints live at /api/market. The previous URL (/api/market/items)
            // targets the admin-only publication endpoints and results in a 401 response.
            // Point the client to the anonymous listings endpoint instead.
            var url = "/api/market";
            if (qs.Count > 0)
                url += "?" + string.Join("&", qs);

            var http = await _clientFactory.CreateAsync();
            using var resp = await http.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            // Optionally read server UTC time header
            if (resp.Headers.TryGetValues("x-server-time-utc", out var values))
            {
                var rawValue = values.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(rawValue)
                    && DateTime.TryParse(
                        rawValue,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var serverUtc))
                {
                    LastServerTimeUtc = serverUtc.ToUniversalTime();
                }
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            var result = JsonSerializer.Deserialize<PagedResult<CoreItemVm>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return result ?? new PagedResult<CoreItemVm>(Array.Empty<CoreItemVm>(), 0);
        }

        public DateTime? LastServerTimeUtc { get; private set; }

        public async Task<CoreItemVm> PlaceBidAsync(BidRequest req, CancellationToken ct)
        {
            var http = await _clientFactory.CreateAsync();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/market/{req.ItemId}/bid");
            request.Headers.TryAddWithoutValidation("X-RowVersion", req.RowVersion);
            request.Content = JsonContent.Create(req);
            using var resp = await http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.Conflict || resp.StatusCode == HttpStatusCode.PreconditionFailed)
                throw new InvalidOperationException("Outbid or stale version.");

            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CoreItemVm>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Failed to deserialize MarketItemVm");
        }

        public async Task<CoreItemVm> BuyNowAsync(BuyNowRequest req, CancellationToken ct)
        {
            var http = await _clientFactory.CreateAsync();
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/market/{req.ItemId}/buy-now");
            request.Headers.TryAddWithoutValidation("X-RowVersion", req.RowVersion);
            request.Content = JsonContent.Create(req);
            using var resp = await http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.Conflict || resp.StatusCode == HttpStatusCode.PreconditionFailed)
                throw new InvalidOperationException("Outbid or stale version.");

            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CoreItemVm>(cancellationToken: ct)
                ?? throw new InvalidOperationException("Failed to deserialize MarketItemVm");
        }
    }
}
