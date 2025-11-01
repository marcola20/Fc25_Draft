using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Fc25Draft.Tests;

public class MarketCycleAdminServiceTests
{
    [Fact]
    public async Task UpdateStatusAsync_ActivatesDraftItems()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var context = CreateDbContext();
        await SeedPositionAndPlayerAsync(context);

        var cycleId = Guid.NewGuid();
        var now = fakeTime.GetUtcNow().UtcDateTime;
        context.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Pré-mercado",
            Status = MarketCycleStatus.Draft,
            StartsAtUtc = now.AddHours(-1),
            EndsAtUtc = now.AddHours(6),
            CreatedAtUtc = now.AddHours(-2),
            UpdatedAtUtc = now.AddHours(-2)
        });

        context.MarketItems.AddRange(
            new MarketItem
            {
                ItemId = Guid.NewGuid(),
                CycleId = cycleId,
                PlayerId = 1,
                BasePrice = 100m,
                MinIncrement = 10m,
                ExpiresAtUtc = now.AddHours(4),
                Status = MarketItemStatus.Draft,
                CreatedAtUtc = now.AddHours(-1),
                LastUpdateUtc = now.AddHours(-1)
            },
            new MarketItem
            {
                ItemId = Guid.NewGuid(),
                CycleId = cycleId,
                PlayerId = 2,
                BasePrice = 120m,
                MinIncrement = 10m,
                ExpiresAtUtc = now.AddHours(3),
                Status = MarketItemStatus.Draft,
                CreatedAtUtc = now.AddHours(-1),
                LastUpdateUtc = now.AddHours(-1)
            });

        await context.SaveChangesAsync();

        var service = CreateService(context, fakeTime);
        await service.UpdateStatusAsync(cycleId, MarketCycleStatus.Active, forceClose: false, CancellationToken.None);

        var reloadedItems = await context.MarketItems
            .Where(i => i.CycleId == cycleId)
            .AsNoTracking()
            .ToListAsync();

        Assert.All(reloadedItems, item => Assert.Equal(MarketItemStatus.Active, item.Status));
        Assert.All(reloadedItems, item => Assert.NotNull(item.PublishedAtUtc));
    }

    [Fact]
    public async Task UpdateStatusAsync_ForceCloseCancelsActiveItems()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 2, 1, 8, 0, 0, TimeSpan.Zero));
        await using var context = CreateDbContext();
        await SeedPositionAndPlayerAsync(context);

        var cycleId = Guid.NewGuid();
        var now = fakeTime.GetUtcNow().UtcDateTime;
        context.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Mercado ativo",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = now.AddHours(-2),
            EndsAtUtc = now.AddHours(2),
            CreatedAtUtc = now.AddHours(-4),
            UpdatedAtUtc = now.AddHours(-2)
        });

        context.MarketItems.Add(new MarketItem
        {
            ItemId = Guid.NewGuid(),
            CycleId = cycleId,
            PlayerId = 1,
            BasePrice = 150m,
            MinIncrement = 15m,
            ExpiresAtUtc = now.AddHours(1),
            Status = MarketItemStatus.Active,
            CreatedAtUtc = now.AddHours(-1),
            LastUpdateUtc = now.AddHours(-1)
        });

        await context.SaveChangesAsync();

        var service = CreateService(context, fakeTime);
        await service.UpdateStatusAsync(cycleId, MarketCycleStatus.Closed, forceClose: true, CancellationToken.None);

        var updatedItem = await context.MarketItems.AsNoTracking().SingleAsync(i => i.CycleId == cycleId);
        Assert.Equal(MarketItemStatus.Canceled, updatedItem.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_IsMonotonicUnderConcurrentRequests()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseSqlite(connection)
            .Options;

        var cycleId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var setupContext = new DraftDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
            setupContext.MarketCycles.Add(new MarketCycle
            {
                CycleId = cycleId,
                Name = "Concorrente",
                Status = MarketCycleStatus.Draft,
                StartsAtUtc = now.AddHours(-1),
                EndsAtUtc = now.AddHours(5),
                CreatedAtUtc = now.AddHours(-2),
                UpdatedAtUtc = now.AddHours(-2)
            });

            await setupContext.SaveChangesAsync();
        }

        var activateTask = Task.Run(async () =>
        {
            await using var ctx = new DraftDbContext(options);
            var service = CreateService(ctx);
            await service.UpdateStatusAsync(cycleId, MarketCycleStatus.Active, forceClose: false, CancellationToken.None);
        });

        var closeTask = Task.Run(async () =>
        {
            await using var ctx = new DraftDbContext(options);
            var service = CreateService(ctx);
            await service.UpdateStatusAsync(cycleId, MarketCycleStatus.Closed, forceClose: true, CancellationToken.None);
        });

        await Task.WhenAll(activateTask, closeTask);

        await using (var verification = new DraftDbContext(options))
        {
            var cycle = await verification.MarketCycles.AsNoTracking().SingleAsync(c => c.CycleId == cycleId);
            Assert.Equal(MarketCycleStatus.Closed, cycle.Status);
        }
    }

    [Fact]
    public async Task ConcludeAsync_AssignsWinningBidAndClosesCycle()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 3, 10, 14, 0, 0, TimeSpan.Zero));
        await using var context = CreateDbContext();
        await SeedPositionAndPlayerAsync(context);

        var cycleId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var now = fakeTime.GetUtcNow().UtcDateTime;

        context.Teams.Add(new Team
        {
            TeamId = teamId,
            TeamName = "Equipe Campeã",
            Token = "TEAM-TOKEN",
            Budget = 5_000m,
            BudgetBlocked = 2_000m
        });

        context.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Ciclo Ativo",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = now.AddHours(-2),
            EndsAtUtc = now.AddHours(4),
            CreatedAtUtc = now.AddHours(-3),
            UpdatedAtUtc = now.AddHours(-3)
        });

        context.MarketItems.Add(new MarketItem
        {
            ItemId = itemId,
            CycleId = cycleId,
            PlayerId = 1,
            BasePrice = 500m,
            MinIncrement = 50m,
            ExpiresAtUtc = now.AddHours(1),
            Status = MarketItemStatus.Active,
            CreatedAtUtc = now.AddHours(-1),
            LastUpdateUtc = now.AddHours(-1),
            CurrentLeaderTeamId = teamId,
            CurrentLeaderAmount = 2_000m
        });

        await context.SaveChangesAsync();

        var settlement = new AuctionSettlementService(
            context,
            new TransactionLogService(context),
            NullLogger<AuctionSettlementService>.Instance,
            fakeTime);

        var service = new MarketCycleAdminService(context, settlement, fakeTime);

        var result = await service.ConcludeAsync(cycleId, CancellationToken.None);

        Assert.Equal(MarketCycleStatus.Closed, result.Cycle.Status);
        Assert.NotNull(result.SettlementSummary);
        Assert.Equal(1, result.SettlementSummary!.Sold);
        Assert.Equal(0, result.SettlementSummary!.Expired);

        var updatedCycle = await context.MarketCycles.AsNoTracking().SingleAsync(c => c.CycleId == cycleId);
        Assert.Equal(MarketCycleStatus.Closed, updatedCycle.Status);
        Assert.Equal(now, updatedCycle.UpdatedAtUtc);

        var item = await context.MarketItems.AsNoTracking().SingleAsync(i => i.ItemId == itemId);
        Assert.Equal(MarketItemStatus.Sold, item.Status);
        Assert.Equal(teamId, item.WinnerTeamId);
        Assert.Equal(2_000m, item.CurrentLeaderAmount);

        var team = await context.Teams.AsNoTracking().SingleAsync(t => t.TeamId == teamId);
        Assert.Equal(3_000m, team.Budget);
        Assert.Equal(0m, team.BudgetBlocked);

        var player = await context.Players.AsNoTracking().SingleAsync(p => p.PlayerId == 1);
        Assert.Equal(teamId, player.CurrentTeamId);

        var rosterEntry = await context.TeamRosters.AsNoTracking()
            .SingleAsync(r => r.TeamId == teamId && r.PlayerId == 1);
        Assert.NotNull(rosterEntry);

        var transfer = await context.TransferHistories.AsNoTracking().SingleAsync();
        Assert.Equal(1, transfer.PlayerId);
        Assert.Equal(teamId, transfer.ToTeamId);
        Assert.Equal(2_000m, transfer.Amount);
        Assert.Equal(DateTimeKind.Utc, transfer.PerformedAtUtc.Kind);
        Assert.Equal(now, transfer.PerformedAtUtc);

        var transaction = await context.MarketTransactions.AsNoTracking().SingleAsync();
        Assert.Equal(MarketTransactionType.AuctionSettled, transaction.Type);
        Assert.Equal(teamId, transaction.TeamId);
        Assert.Equal(DateTimeKind.Utc, transaction.CreatedAtUtc.Kind);
        Assert.Equal(now, transaction.CreatedAtUtc);

        var secondCall = await service.ConcludeAsync(cycleId, CancellationToken.None);
        Assert.NotNull(secondCall.SettlementSummary);
        Assert.Equal(1, secondCall.SettlementSummary!.Sold);
        Assert.Equal(1, await context.TransferHistories.CountAsync());
    }

    private static MarketCycleAdminService CreateService(DraftDbContext context, TimeProvider? timeProvider = null, IAuctionSettlementService? settlementService = null)
        => new(context, settlementService ?? new StubSettlementService(), timeProvider);

    private static DraftDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase($"market-cycle-admin-{Guid.NewGuid():N}")
            .Options;
        var context = new DraftDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static async Task SeedPositionAndPlayerAsync(DraftDbContext context)
    {
        if (!await context.Positions.AnyAsync())
        {
            context.Positions.Add(new Position { PositionId = 1, Name = "Goleiro" });
        }

        if (!await context.Players.AnyAsync(p => p.PlayerId == 1))
        {
            context.Players.Add(new Player
            {
                PlayerId = 1,
                PlayerGuid = Guid.NewGuid(),
                Name = "Jogador teste",
                Overall = 80,
                PositionId = 1
            });
        }

        if (!await context.Players.AnyAsync(p => p.PlayerId == 2))
        {
            context.Players.Add(new Player
            {
                PlayerId = 2,
                PlayerGuid = Guid.NewGuid(),
                Name = "Jogador reserva",
                Overall = 78,
                PositionId = 1
            });
        }

        await context.SaveChangesAsync();
    }

    private sealed class StubSettlementService : IAuctionSettlementService
    {
        public Task<AuctionSettlementResult> SettleExpiredItemsAsync(Guid cycleId, CancellationToken ct)
            => Task.FromResult(new AuctionSettlementResult(0, 0));

        public Task<AuctionSettlementResult> SettleAllOpenItemsOnCycleCloseAsync(Guid cycleId, CancellationToken ct)
            => Task.FromResult(new AuctionSettlementResult(0, 0));
    }
}
