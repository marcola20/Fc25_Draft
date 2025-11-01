using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using System.Linq;

namespace Fc25Draft.Tests;

public class MarketItemGenerationServiceTests
{
    [Fact]
    public async Task PreviewAsync_Throws_WhenCycleNotDraft()
    {
        await using var context = CreateDbContext();
        await SeedPositionAsync(context);
        var cycleId = Guid.NewGuid();
        var cycle = new MarketCycle
        {
            CycleId = cycleId,
            Name = "Aberto",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = DateTime.UtcNow.AddHours(-1),
            EndsAtUtc = DateTime.UtcNow.AddHours(5),
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2),
            UpdatedAtUtc = DateTime.UtcNow.AddHours(-1)
        };

        context.MarketCycles.Add(cycle);
        context.Players.Add(new Player
        {
            PlayerId = 10,
            PlayerGuid = Guid.NewGuid(),
            Name = "Jogador",
            Overall = 80,
            PositionId = 1
        });
        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            1,
            123,
            new MarketItemGenerationFilters(null, null, null, null, null, null),
            new MarketItemLifecycleOptions(null, null, null));

        await Assert.ThrowsAsync<MarketValidationException>(
            () => service.PreviewAsync(cycleId, options, CancellationToken.None));
    }

    [Fact]
    public async Task PreviewAsync_Throws_WhenDesiredExceedsPool()
    {
        await using var context = CreateDbContext();
        await SeedPositionAsync(context);
        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();
        context.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Draft",
            Status = MarketCycleStatus.Draft,
            StartsAtUtc = now,
            EndsAtUtc = now.AddHours(4),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        context.Players.Add(new Player
        {
            PlayerId = 20,
            PlayerGuid = Guid.NewGuid(),
            Name = "Elegível",
            Overall = 78,
            PositionId = 1
        });
        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            2,
            555,
            new MarketItemGenerationFilters(null, null, null, null, null, null),
            new MarketItemLifecycleOptions(null, null, null));

        var ex = await Assert.ThrowsAsync<MarketValidationException>(
            () => service.PreviewAsync(cycleId, options, CancellationToken.None));

        Assert.Contains("quantidade desejada", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_SkipsExistingPlayers()
    {
        await using var context = CreateDbContext();
        await SeedPositionAsync(context);
        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();
        context.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Draft",
            Status = MarketCycleStatus.Draft,
            StartsAtUtc = now,
            EndsAtUtc = now.AddHours(8),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        context.Players.AddRange(
            new Player
            {
                PlayerId = 30,
                PlayerGuid = Guid.NewGuid(),
                Name = "Primeiro",
                Overall = 81,
                PositionId = 1
            },
            new Player
            {
                PlayerId = 31,
                PlayerGuid = Guid.NewGuid(),
                Name = "Segundo",
                Overall = 82,
                PositionId = 1
            });
        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            2,
            99,
            new MarketItemGenerationFilters(null, null, null, null, null, null),
            new MarketItemLifecycleOptions(null, null, null));

        var first = await service.GenerateAsync(cycleId, options, CancellationToken.None);
        Assert.Equal(2, first.CreatedCount);
        Assert.Equal(0, first.SkippedExistingCount);

        var second = await service.GenerateAsync(cycleId, options, CancellationToken.None);
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(options.DesiredCount, second.SkippedExistingCount);
    }

    [Fact]
    public async Task GenerateAsync_IgnoresPlayersAlreadyAssignedToTeams()
    {
        await using var context = CreateDbContext();
        await SeedPositionAsync(context);
        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        context.Teams.Add(new Team
        {
            TeamId = teamId,
            TeamName = "Time A",
            Token = "TOKEN-A",
            Budget = 1_000m,
            BudgetBlocked = 0m
        });

        context.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Draft",
            Status = MarketCycleStatus.Draft,
            StartsAtUtc = now,
            EndsAtUtc = now.AddHours(8),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        context.Players.AddRange(
            new Player
            {
                PlayerId = 40,
                PlayerGuid = Guid.NewGuid(),
                Name = "Livre",
                Overall = 85,
                PositionId = 1
            },
            new Player
            {
                PlayerId = 41,
                PlayerGuid = Guid.NewGuid(),
                Name = "Com Time",
                Overall = 82,
                PositionId = 1,
                CurrentTeamId = teamId
            },
            new Player
            {
                PlayerId = 42,
                PlayerGuid = Guid.NewGuid(),
                Name = "No Roster",
                Overall = 80,
                PositionId = 1
            });

        context.TeamRosters.Add(new TeamRoster
        {
            TeamId = teamId,
            PlayerId = 42
        });

        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            1,
            777,
            new MarketItemGenerationFilters(null, null, null, null, null, null),
            new MarketItemLifecycleOptions(null, null, null));

        var preview = await service.PreviewAsync(cycleId, options, CancellationToken.None);
        Assert.Equal(1, preview.EligibleCount);
        Assert.All(preview.Items, item => Assert.Equal(40, item.PlayerId));

        var result = await service.GenerateAsync(cycleId, options, CancellationToken.None);
        Assert.Equal(1, result.CreatedCount);
        Assert.Equal(0, result.SkippedExistingCount);
        Assert.Single(result.CreatedItems);
        Assert.Equal(40, result.CreatedItems[0].PlayerId);

        var persistedItems = await context.MarketItems.AsNoTracking().Where(i => i.CycleId == cycleId).ToListAsync();
        Assert.Single(persistedItems);
        Assert.Equal(40, persistedItems[0].PlayerId);
    }

    private static DraftDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new DraftDbContext(options);
    }

    private static async Task SeedPositionAsync(DraftDbContext context)
    {
        if (await context.Positions.AnyAsync())
        {
            return;
        }

        context.Positions.Add(new Position
        {
            PositionId = 1,
            Name = "Posição"
        });

        await context.SaveChangesAsync();
    }

    private sealed class FakePricingService : IPricingService
    {
        public PricingResult Calculate(decimal positionWeight, int overall, int age)
        {
            return new PricingResult(overall * 1000m, overall * 10m, overall * 1500m);
        }

        public Task<PricingResult> CalculateForPlayerAsync(int playerId, CancellationToken ct)
        {
            var basePrice = 500_000m + playerId;
            return Task.FromResult(new PricingResult(basePrice, 10_000m, basePrice * 1.5m));
        }

        public Task<PricingResult> CalculateForPositionAsync(string? positionCode, short? positionId, int age, int overall, CancellationToken ct)
        {
            return Task.FromResult(new PricingResult(overall * 1000m, 5_000m, overall * 1500m));
        }

        public decimal Round(decimal value, decimal step) => value;

        public decimal RoundUp(decimal value, decimal step) => value;
    }
}
