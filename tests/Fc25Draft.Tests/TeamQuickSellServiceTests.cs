using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Fc25Draft.Tests;

public class TeamQuickSellServiceTests
{
    [Fact]
    public async Task QuickSellAsync_UpdatesOverallBeforeFreeingAndRecordsHistory()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseSqlite(connection)
            .Options;

        var teamId = Guid.NewGuid();
        var playerGuid = Guid.NewGuid();
        const int targetPlayerId = 1;

        await using (var setup = new DraftDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();

            setup.Positions.Add(new Position { PositionId = 1, Name = "Atacante" });
            setup.Teams.Add(new Team
            {
                TeamId = teamId,
                TeamName = "Time Teste",
                Token = "TEAMTOKEN",
                Budget = 1_000_000m,
                BudgetBlocked = 0m
            });

            for (var i = 0; i < 19; i++)
            {
                var playerId = i + 1;
                var overall = i == 0 ? 85 : 70;
                var guid = i == 0 ? playerGuid : Guid.NewGuid();

                setup.Players.Add(new Player
                {
                    PlayerId = playerId,
                    PlayerGuid = guid,
                    Name = $"Jogador {playerId}",
                    Overall = overall,
                    PositionId = 1,
                    CurrentTeamId = teamId
                });

                setup.TeamRosters.Add(new TeamRoster
                {
                    TeamId = teamId,
                    PlayerId = playerId
                });
            }

            await setup.SaveChangesAsync();
        }

        var fakeNow = new DateTimeOffset(2025, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fakeNow);
        var pricingService = new StubPricingService(250_000m);

        await using var context = new TrackingDraftDbContext(options);
        var service = new TeamQuickSellService(
            context,
            pricingService,
            NullLogger<TeamQuickSellService>.Instance,
            timeProvider);

        var result = await service.QuickSellAsync(teamId, playerGuid, "TEAMTOKEN", CancellationToken.None);

        Assert.Equal(85, result.OldOverall);
        Assert.Equal(86, result.NewOverall);

        Assert.Equal(2, context.SaveChangesSnapshots.Count);
        var firstSnapshot = context.SaveChangesSnapshots[0];
        Assert.Contains(firstSnapshot, entry => entry.EntityName == nameof(Player) && entry.State == EntityState.Modified);
        Assert.DoesNotContain(firstSnapshot, entry => entry.EntityName == nameof(TeamRoster));

        var secondSnapshot = context.SaveChangesSnapshots[1];
        Assert.Contains(secondSnapshot, entry => entry.EntityName == nameof(TeamRoster) && entry.State == EntityState.Deleted);
        Assert.Contains(secondSnapshot, entry => entry.EntityName == nameof(TransferHistory) && entry.State == EntityState.Added);

        var storedPlayer = await context.Players.AsNoTracking().SingleAsync(p => p.PlayerId == targetPlayerId);
        Assert.Equal(86, storedPlayer.Overall);
        Assert.Null(storedPlayer.CurrentTeamId);
        Assert.Null(storedPlayer.QuickSellTeamId);
        Assert.Null(storedPlayer.QuickSellOldOverall);
        Assert.Null(storedPlayer.QuickSellNewOverall);

        var rosterEntry = await context.TeamRosters.AsNoTracking().FirstOrDefaultAsync(r => r.PlayerId == targetPlayerId);
        Assert.Null(rosterEntry);

        var history = await context.TransferHistories.AsNoTracking().SingleAsync();
        Assert.Equal(85, history.OldOverall);
        Assert.Equal(86, history.NewOverall);
        Assert.Equal(fakeNow.UtcDateTime, history.PerformedAtUtc);
    }

    [Fact]
    public async Task QuickSellAsync_ReusesPendingOverallBumpMetadata()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseSqlite(connection)
            .Options;

        var teamId = Guid.NewGuid();
        var playerGuid = Guid.NewGuid();
        const int targetPlayerId = 1;

        await using (var setup = new DraftDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();

            setup.Positions.Add(new Position { PositionId = 1, Name = "Atacante" });
            setup.Teams.Add(new Team
            {
                TeamId = teamId,
                TeamName = "Time Teste",
                Token = "TEAMTOKEN",
                Budget = 1_000_000m,
                BudgetBlocked = 0m
            });

            for (var i = 0; i < 19; i++)
            {
                var playerId = i + 1;
                var isTarget = i == 0;
                var guid = isTarget ? playerGuid : Guid.NewGuid();
                var overall = isTarget ? 86 : 72;

                setup.Players.Add(new Player
                {
                    PlayerId = playerId,
                    PlayerGuid = guid,
                    Name = $"Jogador {playerId}",
                    Overall = overall,
                    PositionId = 1,
                    CurrentTeamId = teamId,
                    QuickSellTeamId = isTarget ? teamId : null,
                    QuickSellOldOverall = isTarget ? 85 : null,
                    QuickSellNewOverall = isTarget ? 86 : null
                });

                setup.TeamRosters.Add(new TeamRoster
                {
                    TeamId = teamId,
                    PlayerId = playerId
                });
            }

            await setup.SaveChangesAsync();
        }

        var fakeNow = new DateTimeOffset(2025, 2, 5, 18, 30, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(fakeNow);
        var pricingService = new StubPricingService(180_000m);

        await using var context = new TrackingDraftDbContext(options);
        var service = new TeamQuickSellService(
            context,
            pricingService,
            NullLogger<TeamQuickSellService>.Instance,
            timeProvider);

        var result = await service.QuickSellAsync(teamId, playerGuid, "TEAMTOKEN", CancellationToken.None);

        Assert.Equal(85, result.OldOverall);
        Assert.Equal(86, result.NewOverall);

        Assert.Single(context.SaveChangesSnapshots);
        var snapshot = context.SaveChangesSnapshots[0];
        Assert.Contains(snapshot, entry => entry.EntityName == nameof(TransferHistory) && entry.State == EntityState.Added);
        Assert.Contains(snapshot, entry => entry.EntityName == nameof(TeamRoster) && entry.State == EntityState.Deleted);

        var storedPlayer = await context.Players.AsNoTracking().SingleAsync(p => p.PlayerId == targetPlayerId);
        Assert.Equal(86, storedPlayer.Overall);
        Assert.Null(storedPlayer.CurrentTeamId);
        Assert.Null(storedPlayer.QuickSellTeamId);
        Assert.Null(storedPlayer.QuickSellOldOverall);
        Assert.Null(storedPlayer.QuickSellNewOverall);

        var history = await context.TransferHistories.AsNoTracking().SingleAsync();
        Assert.Equal(85, history.OldOverall);
        Assert.Equal(86, history.NewOverall);
        Assert.Equal(fakeNow.UtcDateTime, history.PerformedAtUtc);
    }

    private sealed record EntityStateSnapshot(string EntityName, EntityState State);

    private sealed class TrackingDraftDbContext : DraftDbContext
    {
        public List<IReadOnlyCollection<EntityStateSnapshot>> SaveChangesSnapshots { get; } = new();

        public TrackingDraftDbContext(DbContextOptions<DraftDbContext> options)
            : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            CaptureSnapshot();
            return base.SaveChangesAsync(cancellationToken);
        }

        private void CaptureSnapshot()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached)
                .Select(e => new EntityStateSnapshot(e.Entity.GetType().Name, e.State))
                .ToArray();

            if (entries.Length > 0)
            {
                SaveChangesSnapshots.Add(entries);
            }
        }
    }

    private sealed class StubPricingService : IPricingService
    {
        private readonly PricingResult _result;

        public StubPricingService(decimal basePrice)
        {
            _result = new PricingResult(basePrice, basePrice * 0.1m, basePrice * 1.5m);
        }

        public PricingResult Calculate(decimal positionWeight, int overall, int age) => _result;

        public Task<PricingResult> CalculateForPositionAsync(string? positionCode, short? positionId, int age, int overall, CancellationToken ct)
            => Task.FromResult(_result);

        public Task<PricingResult> CalculateForPlayerAsync(int playerId, CancellationToken ct)
            => Task.FromResult(_result);
    }
}
