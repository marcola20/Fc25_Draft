using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fc25Draft.Tests;

public class MarketServiceTests
{
    [Fact]
    public async Task GenerateRoundAsync_SelectsConfiguredDistribution()
    {
        using var context = CreateDbContext();
        SeedPositions(context);

        var players = new List<Player>();
        for (var i = 0; i < 6; i++)
        {
            players.Add(new Player
            {
                PlayerId = 100 + i,
                Name = $"Common {i}",
                Age = 25,
                Overall = 77 + (i % 3),
                PositionId = 1
            });
        }

        players.Add(new Player
        {
            PlayerId = 200,
            Name = "Intermediate",
            Age = 27,
            Overall = 80,
            PositionId = 1
        });

        players.Add(new Player
        {
            PlayerId = 300,
            Name = "Rare",
            Age = 24,
            Overall = 90,
            PositionId = 1
        });

        context.Players.AddRange(players);
        await context.SaveChangesAsync();

        var pricingService = CreatePricingService(players);
        var options = Options.Create(new MarketGenerationOptions());
        var service = new MarketService(context, pricingService, options, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var items = await service.GenerateRoundAsync(CancellationToken.None);

        Assert.Equal(8, items.Count);
        Assert.All(items, x => Assert.Equal("OPEN", x.Status));
        Assert.Equal(6, items.Count(x => x.Player.Overall is >= 77 and <= 79));
        Assert.Equal(1, items.Count(x => x.Player.Overall is >= 80 and <= 81));
        Assert.Equal(1, items.Count(x => x.Player.Overall is >= 82));

        var generatedPlayerIds = items.Select(x => x.PlayerId).ToList();
        Assert.Equal(8, generatedPlayerIds.Distinct().Count());
        Assert.True(generatedPlayerIds.All(id => pricingService.RequestedPlayerIds.Contains(id)));
        Assert.Equal(8, await context.TransferMarketItems.CountAsync());
    }

    [Fact]
    public async Task GenerateRoundAsync_ThrowsWhenPoolInsufficient()
    {
        using var context = CreateDbContext();
        SeedPositions(context);

        for (var i = 0; i < 5; i++)
        {
            context.Players.Add(new Player
            {
                PlayerId = 10 + i,
                Name = $"Common {i}",
                Age = 26,
                Overall = 77,
                PositionId = 1
            });
        }

        await context.SaveChangesAsync();

        var pricingService = CreatePricingService(context.Players);
        var options = Options.Create(new MarketGenerationOptions());
        var service = new MarketService(context, pricingService, options, new FixedTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<MarketGenerationValidationException>(() => service.GenerateRoundAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GenerateRoundAsync_RespectsProtectionWindow()
    {
        var now = DateTimeOffset.UtcNow;
        using var context = CreateDbContext();
        SeedPositions(context);

        var protectedPlayer = new Player
        {
            PlayerId = 999,
            Name = "Protected",
            Age = 28,
            Overall = 78,
            PositionId = 1
        };

        context.Players.Add(protectedPlayer);
        context.TransferMarketItems.Add(new TransferMarketItem
        {
            MarketItemId = Guid.NewGuid(),
            PlayerId = protectedPlayer.PlayerId,
            PrecoBase = 10,
            PrecoComprarAgora = 15,
            Status = "OPEN",
            DataInicioUtc = now.UtcDateTime.AddMinutes(-2)
        });
        await context.SaveChangesAsync();

        var pricingService = CreatePricingService(context.Players);
        var options = Options.Create(new MarketGenerationOptions());
        var service = new MarketService(context, pricingService, options, new FixedTimeProvider(now));

        await Assert.ThrowsAsync<MarketGenerationConflictException>(() => service.GenerateRoundAsync(CancellationToken.None));
    }

    [Fact]
    public async Task GenerateRoundAsync_DoesNotSelectPlayersWithExistingOpenItem()
    {
        using var context = CreateDbContext();
        SeedPositions(context);

        var blockedPlayer = new Player
        {
            PlayerId = 50,
            Name = "Blocked",
            Age = 27,
            Overall = 77,
            PositionId = 1
        };

        var commonPlayers = Enumerable.Range(0, 6).Select(i => new Player
        {
            PlayerId = 100 + i,
            Name = $"Player {i}",
            Age = 25,
            Overall = 77 + (i % 3),
            PositionId = 1
        }).ToList();

        var intermediatePlayer = new Player
        {
            PlayerId = 250,
            Name = "Intermediate",
            Age = 26,
            Overall = 80,
            PositionId = 1
        };

        var rarePlayer = new Player
        {
            PlayerId = 300,
            Name = "Rare",
            Age = 23,
            Overall = 95,
            PositionId = 1
        };

        context.Players.Add(blockedPlayer);
        context.Players.AddRange(commonPlayers);
        context.Players.Add(intermediatePlayer);
        context.Players.Add(rarePlayer);
        context.TransferMarketItems.Add(new TransferMarketItem
        {
            MarketItemId = Guid.NewGuid(),
            PlayerId = blockedPlayer.PlayerId,
            PrecoBase = 12,
            PrecoComprarAgora = 18,
            Status = "OPEN",
            DataInicioUtc = DateTime.UtcNow.AddHours(-1)
        });
        await context.SaveChangesAsync();

        var pricingService = CreatePricingService(context.Players);
        var options = Options.Create(new MarketGenerationOptions());
        var service = new MarketService(context, pricingService, options, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var result = await service.GenerateRoundAsync(CancellationToken.None);

        Assert.DoesNotContain(result, item => item.PlayerId == blockedPlayer.PlayerId);
    }

    private static DraftDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DraftDbContext(options);
    }

    private static void SeedPositions(DraftDbContext context)
    {
        context.Positions.Add(new Position { PositionId = 1, Name = "Goleiro" });
        context.SaveChanges();
    }

    private static FakePricingService CreatePricingService(IEnumerable<Player> players)
    {
        var map = players.ToDictionary(
            p => p.PlayerId,
            p => new PricingResult(p.Overall, p.Overall * 0.8m, p.Overall * 1.5m));

        return new FakePricingService(map);
    }

    private sealed class FakePricingService : IPricingService
    {
        private readonly Dictionary<int, PricingResult> _map;

        public FakePricingService(Dictionary<int, PricingResult> map)
        {
            _map = map;
        }

        public HashSet<int> RequestedPlayerIds { get; } = new();

        public Task<PricingResult> CalculateAsync(string? positionCode, short? positionId, int age, int overall, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PricingResult(0, 0, 0));

        public Task<PricingResult> CalculateForPlayerAsync(int playerId, CancellationToken cancellationToken = default)
        {
            RequestedPlayerIds.Add(playerId);
            return Task.FromResult(_map[playerId]);
        }

        public decimal RoundToTenth(decimal value) => value;

        public decimal NextMinIncrement(decimal currentBid) => 0.1m;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
