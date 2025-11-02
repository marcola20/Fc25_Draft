using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

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
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            true,
            123,
            null,
            null,
            true);

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
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            true,
            555,
            null,
            null,
            true);

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
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            true,
            99,
            null,
            null,
            true);

        var first = await service.GenerateAsync(cycleId, options, CancellationToken.None);
        Assert.Equal(2, first.CreatedCount);
        Assert.Empty(first.Skipped);

        var second = await service.GenerateAsync(cycleId, options, CancellationToken.None);
        Assert.Equal(0, second.CreatedCount);
        Assert.Equal(options.DesiredCount, second.Skipped.Count);
    }

    [Fact]
    public async Task GenerateAsync_RespectsMaxPerTeam()
    {
        await using var context = CreateDbContext();
        await SeedPositionAsync(context);
        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();

        context.Teams.AddRange(
            new Team { TeamId = teamA, TeamName = "Time A", Token = "TOKEN-A", Budget = 1_000m, BudgetBlocked = 0m },
            new Team { TeamId = teamB, TeamName = "Time B", Token = "TOKEN-B", Budget = 1_000m, BudgetBlocked = 0m });

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
                Name = "Jogador A1",
                Overall = 85,
                PositionId = 1,
                CurrentTeamId = teamA
            },
            new Player
            {
                PlayerId = 41,
                PlayerGuid = Guid.NewGuid(),
                Name = "Jogador A2",
                Overall = 82,
                PositionId = 1,
                CurrentTeamId = teamA
            },
            new Player
            {
                PlayerId = 42,
                PlayerGuid = Guid.NewGuid(),
                Name = "Jogador B1",
                Overall = 80,
                PositionId = 1,
                CurrentTeamId = teamB
            });
        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            2,
            null,
            null,
            null,
            null,
            null,
            1,
            true,
            true,
            888,
            null,
            null,
            true);

        var result = await service.GenerateAsync(cycleId, options, CancellationToken.None);

        Assert.Equal(2, result.CreatedCount);
        var groupedByTeam = result.Items.GroupBy(i => i.TeamId).ToDictionary(g => g.Key, g => g.Count());
        Assert.True(groupedByTeam.GetValueOrDefault(teamA) <= 1);
        Assert.True(groupedByTeam.GetValueOrDefault(teamB) <= 1);
    }

    [Fact]
    public async Task GenerateAsync_IgnoresPlayersAssignedToTeams()
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

        var availablePlayer = new Player
        {
            PlayerId = 50,
            PlayerGuid = Guid.NewGuid(),
            Name = "Disponível",
            Overall = 83,
            PositionId = 1
        };

        var rosteredPlayer = new Player
        {
            PlayerId = 51,
            PlayerGuid = Guid.NewGuid(),
            Name = "Escalado",
            Overall = 84,
            PositionId = 1
        };

        context.Players.AddRange(availablePlayer, rosteredPlayer);
        await context.SaveChangesAsync();

        context.TeamRosters.Add(new TeamRoster
        {
            TeamId = teamId,
            PlayerId = rosteredPlayer.PlayerId
        });

        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            1,
            null,
            null,
            null,
            null,
            null,
            null,
            true,
            true,
            321,
            null,
            null,
            true);

        var result = await service.GenerateAsync(cycleId, options, CancellationToken.None);

        Assert.Equal(1, result.CreatedCount);
        Assert.Contains(result.Items, item => item.PlayerId == availablePlayer.PlayerId);
        Assert.DoesNotContain(result.Items, item => item.PlayerId == rosteredPlayer.PlayerId);
    }

    [Fact]
    public async Task GenerateAsync_FiltersByAgeRange()
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
            EndsAtUtc = now.AddHours(6),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        context.Players.AddRange(
            new Player
            {
                PlayerId = 70,
                PlayerGuid = Guid.NewGuid(),
                Name = "Veterano",
                Overall = 84,
                PositionId = 1,
                Age = 33
            },
            new Player
            {
                PlayerId = 71,
                PlayerGuid = Guid.NewGuid(),
                Name = "Jovem",
                Overall = 82,
                PositionId = 1,
                Age = 21
            },
            new Player
            {
                PlayerId = 72,
                PlayerGuid = Guid.NewGuid(),
                Name = "Alvo",
                Overall = 83,
                PositionId = 1,
                Age = 26
            });

        await context.SaveChangesAsync();

        var service = new MarketItemGenerationService(context, new FakePricingService(), new FakeTimeProvider(DateTimeOffset.UtcNow));
        var options = new MarketItemGenerationOptions(
            2,
            null,
            null,
            null,
            24,
            28,
            null,
            true,
            true,
            42,
            null,
            null,
            true);

        var result = await service.GenerateAsync(cycleId, options, CancellationToken.None);

        Assert.Equal(1, result.CreatedCount);
        var generated = Assert.Single(result.Items);
        Assert.Equal(72, generated.PlayerId);
    }

    private static DraftDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DraftDbContext(options);
    }

    private static async Task SeedPositionAsync(DraftDbContext context)
    {
        if (!await context.Positions.AnyAsync())
        {
            context.Positions.Add(new Position { PositionId = 1, PositionName = "Atacante" });
            await context.SaveChangesAsync();
        }
    }

    private sealed class FakePricingService : IPricingService
    {
        public Task<PricingResult> CalculateForPlayerAsync(int playerId, CancellationToken ct = default)
        {
            return Task.FromResult(new PricingResult(100m + playerId, 200m + playerId, 10m));
        }
    }
}
