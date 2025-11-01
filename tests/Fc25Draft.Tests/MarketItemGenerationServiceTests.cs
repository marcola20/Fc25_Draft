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
        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            1,
            123,
            new MarketItemGenerationFilters(null, null, null),
            null,
            true,
            true,
            new MarketItemExpirationOptions(true, null, null));

        await Assert.ThrowsAsync<MarketValidationException>(
            () => service.PreviewAsync(cycleId, options, CancellationToken.None));
    }

    [Fact]
    public async Task PreviewAsync_ReturnsSummary_WhenRequestedExceedsEligible()
    {
        await using var context = CreateDbContext();
        await SeedPositionAsync(context);
        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

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

        context.Teams.Add(new Team { TeamId = teamId, TeamName = "Time", Token = "TK", Budget = 1000m, BudgetBlocked = 0m });

        context.Players.Add(new Player
        {
            PlayerId = 20,
            PlayerGuid = Guid.NewGuid(),
            Name = "Elegível",
            Overall = 78,
            PositionId = 1,
            Age = 25
        });

        context.TeamRosters.Add(new TeamRoster { TeamId = teamId, PlayerId = 20 });
        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            2,
            555,
            new MarketItemGenerationFilters(null, null, null),
            null,
            true,
            true,
            new MarketItemExpirationOptions(true, null, null));

        var preview = await service.PreviewAsync(cycleId, options, CancellationToken.None);

        Assert.Equal(2, preview.RequestedCount);
        Assert.Equal(1, preview.EligibleCount);
        Assert.Equal(1, preview.GeneratedCount);
        Assert.Equal(1, preview.SkippedCount);
    }

    [Fact]
    public async Task GenerateAsync_SkipsPlayersAlreadyGenerated()
    {
        await using var context = CreateDbContext();
        await SeedPositionAsync(context);
        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        context.Teams.Add(new Team { TeamId = teamId, TeamName = "Time", Token = "TK", Budget = 1000m, BudgetBlocked = 0m });
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
                PositionId = 1,
                Age = 26
            },
            new Player
            {
                PlayerId = 31,
                PlayerGuid = Guid.NewGuid(),
                Name = "Segundo",
                Overall = 82,
                PositionId = 1,
                Age = 27
            });

        context.TeamRosters.AddRange(
            new TeamRoster { TeamId = teamId, PlayerId = 30 },
            new TeamRoster { TeamId = teamId, PlayerId = 31 });
        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            2,
            99,
            new MarketItemGenerationFilters(null, null, null),
            null,
            true,
            true,
            new MarketItemExpirationOptions(true, null, null));

        var first = await service.GenerateAsync(cycleId, options, CancellationToken.None);
        Assert.Equal(2, first.GeneratedCount);
        Assert.Equal(0, first.SkippedCount);

        var second = await service.GenerateAsync(cycleId, options, CancellationToken.None);
        Assert.Equal(0, second.GeneratedCount);
        Assert.Equal(2, second.SkippedCount);
    }

    [Fact]
    public async Task GenerateAsync_RespectsMaxPerTeamLimit()
    {
        await using var context = CreateDbContext();
        await SeedPositionAsync(context);
        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        context.Teams.AddRange(
            new Team { TeamId = teamA, TeamName = "A", Token = "A", Budget = 1000m, BudgetBlocked = 0m },
            new Team { TeamId = teamB, TeamName = "B", Token = "B", Budget = 1000m, BudgetBlocked = 0m });

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
            new Player { PlayerId = 40, PlayerGuid = Guid.NewGuid(), Name = "A1", Overall = 85, PositionId = 1, Age = 25 },
            new Player { PlayerId = 41, PlayerGuid = Guid.NewGuid(), Name = "A2", Overall = 86, PositionId = 1, Age = 26 },
            new Player { PlayerId = 42, PlayerGuid = Guid.NewGuid(), Name = "B1", Overall = 83, PositionId = 1, Age = 27 });

        context.TeamRosters.AddRange(
            new TeamRoster { TeamId = teamA, PlayerId = 40 },
            new TeamRoster { TeamId = teamA, PlayerId = 41 },
            new TeamRoster { TeamId = teamB, PlayerId = 42 });
        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            3,
            101,
            new MarketItemGenerationFilters(null, null, null),
            1,
            true,
            true,
            new MarketItemExpirationOptions(true, null, null));

        var result = await service.GenerateAsync(cycleId, options, CancellationToken.None);

        Assert.Equal(2, result.GeneratedCount);
        Assert.Equal(1, result.SkippedCount);
        var fromTeamA = result.Items.Count(i => i.PlayerId is 40 or 41);
        Assert.True(fromTeamA <= 1);
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
