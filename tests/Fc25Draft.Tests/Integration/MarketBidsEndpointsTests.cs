using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fc25Draft.Tests.Integration;

public class MarketBidsEndpointsTests : IClassFixture<MarketItemsEndpointsFactory>
{
    private readonly MarketItemsEndpointsFactory _factory;

    public MarketBidsEndpointsTests(MarketItemsEndpointsFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostBid_ReturnsUpdatedItem_WhenValid()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var scenario = await SeedBidScenarioAsync(
            factory,
            basePrice: 28_684_000m,
            minIncrement: 1_000_000m,
            currentLeaderAmount: null,
            buyNowPrice: 32_000_000m,
            teamBudget: 60_000_000m);

        var client = factory.CreateClient();
        using var request = BuildBidRequest(scenario, amount: 30_000_000m);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<MarketItemDto>(GetJsonOptions());
        Assert.NotNull(dto);
        Assert.Equal(30_000_000m, dto!.CurrentLeaderAmount);
        Assert.Equal(scenario.TeamId, dto.CurrentLeaderTeamId);

        var expectedMin = MarketPricing.ComputeRequiredMinBid(
            scenario.BasePrice,
            scenario.MinIncrement,
            dto.CurrentLeaderAmount,
            scenario.BuyNowPrice);
        Assert.Equal(expectedMin, dto.RequiredMinBid);
    }

    [Fact]
    public async Task PostBid_ReturnsBadRequest_WhenBelowMinimum()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var scenario = await SeedBidScenarioAsync(
            factory,
            basePrice: 10_000_000m,
            minIncrement: 1_000_000m,
            currentLeaderAmount: null,
            buyNowPrice: 50_000_000m,
            teamBudget: 80_000_000m);

        var minimum = MarketPricing.ComputeRequiredMinBid(
            scenario.BasePrice,
            scenario.MinIncrement,
            scenario.CurrentLeaderAmount,
            scenario.BuyNowPrice);

        using var request = BuildBidRequest(scenario, amount: minimum - 0.01m);
        var client = factory.CreateClient();
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(GetJsonOptions());
        Assert.NotNull(problem);
        Assert.Contains("lance", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostBid_ReturnsConflict_WhenBudgetInsufficient()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var scenario = await SeedBidScenarioAsync(
            factory,
            basePrice: 5_000_000m,
            minIncrement: 500_000m,
            currentLeaderAmount: null,
            buyNowPrice: 50_000_000m,
            teamBudget: 2_000_000m);

        var minimum = MarketPricing.ComputeRequiredMinBid(
            scenario.BasePrice,
            scenario.MinIncrement,
            scenario.CurrentLeaderAmount,
            scenario.BuyNowPrice);

        using var request = BuildBidRequest(scenario, amount: minimum);
        var client = factory.CreateClient();
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(GetJsonOptions());
        Assert.NotNull(problem);
        Assert.Contains("saldo", problem!.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostBid_ReturnsBadRequest_WhenTeamAlreadyLeads()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var scenario = await SeedBidScenarioAsync(
            factory,
            basePrice: 15_000_000m,
            minIncrement: 500_000m,
            currentLeaderAmount: 16_000_000m,
            buyNowPrice: 40_000_000m,
            teamBudget: 80_000_000m);

        var client = factory.CreateClient();
        var minimum = MarketPricing.ComputeRequiredMinBid(
            scenario.BasePrice,
            scenario.MinIncrement,
            scenario.CurrentLeaderAmount,
            scenario.BuyNowPrice);

        using var request = BuildBidRequest(scenario, minimum);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(GetJsonOptions());
        Assert.NotNull(problem);
        var detail = problem!.Detail
            ?? problem.Title
            ?? (problem.Extensions.TryGetValue("message", out var extension) ? extension?.ToString() : null)
            ?? string.Empty;
        Assert.Contains("already leads", detail, StringComparison.OrdinalIgnoreCase);
    }

    private static HttpRequestMessage BuildBidRequest(BidScenario scenario, decimal amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/market/items/{scenario.ItemId}/bids")
        {
            Content = JsonContent.Create(new { amount })
        };

        var rowVersion = scenario.RowVersion.ToString(CultureInfo.InvariantCulture);
        request.Headers.TryAddWithoutValidation("X-Team-Token", scenario.TeamToken);
        request.Headers.TryAddWithoutValidation("X-RowVersion", rowVersion);
        request.Headers.TryAddWithoutValidation("If-Match", $"W/\"{rowVersion}\"");

        return request;
    }

    private static JsonSerializerOptions GetJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static async Task<BidScenario> SeedBidScenarioAsync(
        WebApplicationFactory<Program> factory,
        decimal basePrice,
        decimal minIncrement,
        decimal? currentLeaderAmount,
        decimal? buyNowPrice,
        decimal teamBudget)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
        await db.Database.EnsureCreatedAsync();

        if (!await db.Positions.AnyAsync(p => p.PositionId == 1))
        {
            db.Positions.Add(new Position { PositionId = 1, Name = "Goleiro" });
        }

        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();
        db.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Ciclo Teste",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = now.AddHours(-1),
            EndsAtUtc = now.AddHours(2),
            CreatedAtUtc = now.AddHours(-2),
            UpdatedAtUtc = now.AddMinutes(-30)
        });

        db.Players.Add(new Player
        {
            PlayerId = 9000,
            Name = "Jogador Teste",
            Overall = 90,
            PositionId = 1,
            PlayerGuid = Guid.NewGuid()
        });

        var teamId = Guid.NewGuid();
        var teamToken = $"token-{Guid.NewGuid():N}";
        db.Teams.Add(new Team
        {
            TeamId = teamId,
            TeamName = "Time Azul",
            Token = teamToken,
            Budget = teamBudget,
            BudgetBlocked = 0m
        });

        var itemId = Guid.NewGuid();
        db.MarketItems.Add(new MarketItem
        {
            ItemId = itemId,
            CycleId = cycleId,
            PlayerId = 9000,
            BasePrice = basePrice,
            BuyNowPrice = buyNowPrice,
            MinIncrement = minIncrement,
            ExpiresAtUtc = now.AddHours(1),
            Status = MarketItemStatus.Active,
            CreatedAtUtc = now.AddHours(-1),
            LastUpdateUtc = now.AddMinutes(-10),
            PublishedAtUtc = now.AddHours(-1),
            CurrentLeaderTeamId = currentLeaderAmount.HasValue ? teamId : null,
            CurrentLeaderAmount = currentLeaderAmount,
            RowVersion = 1
        });

        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        return new BidScenario(
            itemId,
            cycleId,
            teamId,
            teamToken,
            1u,
            basePrice,
            minIncrement,
            currentLeaderAmount,
            buyNowPrice);
    }

    private sealed record BidScenario(
        Guid ItemId,
        Guid CycleId,
        Guid TeamId,
        string TeamToken,
        uint RowVersion,
        decimal BasePrice,
        decimal MinIncrement,
        decimal? CurrentLeaderAmount,
        decimal? BuyNowPrice);
}
