using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Fc25Draft.Tests.Integration;

public class MarketItemsEndpointsTests : IClassFixture<MarketItemsEndpointsFactory>
{
    private readonly MarketItemsEndpointsFactory _factory;

    public MarketItemsEndpointsTests(MarketItemsEndpointsFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetItems_ReturnsConflict_WhenCycleIsDraft()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var cycleId = await SeedDraftCycleAsync(factory);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/market/items?cycleId={cycleId:D}&page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.Contains("ainda não está ativo", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetItems_AfterActivation_DoesNotReturnCanceledStatus()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var cycleId = await SeedDraftCycleAsync(factory);

        using (var scope = factory.Services.CreateScope())
        {
            var adminService = scope.ServiceProvider.GetRequiredService<IMarketCycleAdminService>();
            await adminService.UpdateStatusAsync(cycleId, MarketCycleStatus.Active, forceClose: false, CancellationToken.None);
        }

        var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/market/items?cycleId={cycleId:D}&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = await response.Content.ReadFromJsonAsync<PagedResult<MarketItemListDto>>(options);

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Items);
        Assert.All(result.Items, item => Assert.NotEqual(MarketItemStatus.Canceled, item.Status));
        Assert.DoesNotContain(result.Items, item => string.Equals(item.StatusText, "Cancelado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetItems_ReturnsFilteredResults()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var cycleId = await SeedCycleAsync(factory);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/market/items?cycleId={cycleId:D}&status=Ativo&overallMin=80&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = await response.Content.ReadFromJsonAsync<PagedResult<MarketItemListDto>>(options);

        Assert.NotNull(result);
        Assert.Equal(1, result!.Items.Count);
        var item = result.Items[0];
        Assert.Equal("Jogador 100", item.PlayerName);
        Assert.Equal("Ativo", item.StatusText);
        Assert.True(item.CurrentBid.HasValue);
        Assert.Equal(500m, item.CurrentBid);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task GetItems_SortsByCurrentBidDescending()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var cycleId = await SeedCycleAsync(factory);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/market/items?cycleId={cycleId:D}&sortBy=currentBid&sortOrder=desc&page=1&pageSize=10");
        response.EnsureSuccessStatusCode();

        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var result = await response.Content.ReadFromJsonAsync<PagedResult<MarketItemListDto>>(options);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Items.Count);
        var orderedIds = result.Items.Select(i => i.PlayerId).ToArray();
        Assert.Equal(new[] { 101, 100, 102 }, orderedIds);
        var soldItem = result.Items.First(i => i.PlayerId == 101);
        Assert.False(soldItem.IsActive);
    }

    private static async Task<Guid> SeedCycleAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Positions.AnyAsync(p => p.PositionId == 50))
        {
            db.Positions.Add(new Position { PositionId = 50, Name = "Teste" });
        }

        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();

        db.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Ciclo Público",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = now.AddHours(-1),
            EndsAtUtc = now.AddDays(1),
            CreatedAtUtc = now.AddHours(-2),
            UpdatedAtUtc = now.AddMinutes(-30)
        });

        var players = new[]
        {
            new Player { PlayerId = 100, Name = "Jogador 100", Overall = 85, PositionId = 50, PlayerGuid = Guid.NewGuid() },
            new Player { PlayerId = 101, Name = "Jogador 101", Overall = 88, PositionId = 50, PlayerGuid = Guid.NewGuid() },
            new Player { PlayerId = 102, Name = "Jogador 102", Overall = 72, PositionId = 50, PlayerGuid = Guid.NewGuid() }
        };

        foreach (var player in players)
        {
            if (!await db.Players.AnyAsync(p => p.PlayerId == player.PlayerId))
            {
                db.Players.Add(player);
            }
        }

        db.MarketItems.AddRange(
            new MarketItem
            {
                ItemId = Guid.NewGuid(),
                CycleId = cycleId,
                PlayerId = 100,
                BasePrice = 300m,
                BuyNowPrice = 900m,
                MinIncrement = 50m,
                ExpiresAtUtc = now.AddHours(6),
                Status = MarketItemStatus.Active,
                CreatedAtUtc = now,
                LastUpdateUtc = now,
                PublishedAtUtc = now,
                CurrentLeaderAmount = 500m,
                RowVersion = 1
            },
            new MarketItem
            {
                ItemId = Guid.NewGuid(),
                CycleId = cycleId,
                PlayerId = 101,
                BasePrice = 400m,
                BuyNowPrice = 950m,
                MinIncrement = 50m,
                ExpiresAtUtc = now.AddHours(5),
                Status = MarketItemStatus.Sold,
                CreatedAtUtc = now,
                LastUpdateUtc = now,
                PublishedAtUtc = now,
                CurrentLeaderAmount = 700m,
                RowVersion = 1
            },
            new MarketItem
            {
                ItemId = Guid.NewGuid(),
                CycleId = cycleId,
                PlayerId = 102,
                BasePrice = 200m,
                BuyNowPrice = 800m,
                MinIncrement = 25m,
                ExpiresAtUtc = now.AddHours(4),
                Status = MarketItemStatus.Active,
                CreatedAtUtc = now,
                LastUpdateUtc = now,
                PublishedAtUtc = now,
                CurrentLeaderAmount = null,
                RowVersion = 1
            });

        await db.SaveChangesAsync();
        return cycleId;
    }

    private static async Task<Guid> SeedDraftCycleAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Positions.AnyAsync(p => p.PositionId == 60))
        {
            db.Positions.Add(new Position { PositionId = 60, Name = "Teste Draft" });
        }

        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();

        db.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Ciclo Rascunho",
            Status = MarketCycleStatus.Draft,
            StartsAtUtc = now.AddHours(-1),
            EndsAtUtc = now.AddDays(1),
            CreatedAtUtc = now.AddHours(-2),
            UpdatedAtUtc = now.AddHours(-2)
        });

        var players = new[]
        {
            new Player { PlayerId = 200, Name = "Jogador Draft 1", Overall = 82, PositionId = 60, PlayerGuid = Guid.NewGuid() },
            new Player { PlayerId = 201, Name = "Jogador Draft 2", Overall = 79, PositionId = 60, PlayerGuid = Guid.NewGuid() }
        };

        foreach (var player in players)
        {
            if (!await db.Players.AnyAsync(p => p.PlayerId == player.PlayerId))
            {
                db.Players.Add(player);
            }
        }

        db.MarketItems.AddRange(
            new MarketItem
            {
                ItemId = Guid.NewGuid(),
                CycleId = cycleId,
                PlayerId = 200,
                BasePrice = 250m,
                BuyNowPrice = 800m,
                MinIncrement = 25m,
                ExpiresAtUtc = now.AddHours(5),
                Status = MarketItemStatus.Draft,
                CreatedAtUtc = now,
                LastUpdateUtc = now
            },
            new MarketItem
            {
                ItemId = Guid.NewGuid(),
                CycleId = cycleId,
                PlayerId = 201,
                BasePrice = 300m,
                BuyNowPrice = 900m,
                MinIncrement = 30m,
                ExpiresAtUtc = now.AddHours(6),
                Status = MarketItemStatus.Draft,
                CreatedAtUtc = now,
                LastUpdateUtc = now
            });

        await db.SaveChangesAsync();
        return cycleId;
    }
}

public sealed class MarketItemsEndpointsFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<DraftDbContext>>();
            services.AddDbContext<DraftDbContext>(options =>
                options.UseInMemoryDatabase($"market-items-query-{Guid.NewGuid():N}"));
        });
    }
}
