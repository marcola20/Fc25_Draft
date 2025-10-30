using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Fc25Draft.Tests.Integration;

public class MarketEndpointsTests
{
    [Fact]
    public async Task GetMarket_ReturnsEmptyArray_WhenServiceReturnsNull()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMarketService>();
                services.AddSingleton<IMarketService>(new EmptyMarketService());
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/market");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<List<MarketItemDto>>();
        Assert.NotNull(payload);
        Assert.Empty(payload!);
    }

    [Fact]
    public async Task GetMarket_ReturnsProblemDetails_WhenServiceThrows()
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMarketService>();
                services.AddSingleton<IMarketService>(new ThrowingMarketService());
            });
        });

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/market");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("x-correlation-id", out var correlationValues));
        Assert.False(string.IsNullOrWhiteSpace(correlationValues!.First()));

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal("https://httpstatuses.com/500", problem!.Type);
        Assert.Equal("Unable to load market items.", problem.Title);
        Assert.Equal("/api/market", problem.Instance);
        Assert.True(problem.Extensions.TryGetValue("correlationId", out var correlationId));
        Assert.False(string.IsNullOrWhiteSpace(correlationId?.ToString()));
    }

    private class EmptyMarketService : IMarketService
    {
        public Task<MarketCycleDto> EnsureCycleAsync(CancellationToken ct)
            => Task.FromResult(new MarketCycleDto(Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow));

        public virtual Task<List<MarketItemDto>> GetActiveItemsAsync(CancellationToken ct)
            => Task.FromResult(new List<MarketItemDto>());

        public Task<MarketItemDto?> GetItemAsync(Guid itemId, CancellationToken ct)
            => Task.FromResult<MarketItemDto?>(null);

        public Task<BidResultDto> PlaceBidAsync(Guid itemId, string teamToken, decimal amount, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<BuyNowResultDto> BuyNowAsync(Guid itemId, string teamToken, CancellationToken ct)
            => throw new NotImplementedException();

        public Task<int> CloseExpiredItemsAsync(CancellationToken ct)
            => Task.FromResult(0);
    }

    private sealed class ThrowingMarketService : EmptyMarketService
    {
        public override Task<List<MarketItemDto>> GetActiveItemsAsync(CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }
}
