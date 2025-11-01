using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

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

        var service = new MarketCycleAdminService(context, fakeTime);
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

        var service = new MarketCycleAdminService(context, fakeTime);
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
            var service = new MarketCycleAdminService(ctx);
            await service.UpdateStatusAsync(cycleId, MarketCycleStatus.Active, forceClose: false, CancellationToken.None);
        });

        var closeTask = Task.Run(async () =>
        {
            await using var ctx = new DraftDbContext(options);
            var service = new MarketCycleAdminService(ctx);
            await service.UpdateStatusAsync(cycleId, MarketCycleStatus.Closed, forceClose: true, CancellationToken.None);
        });

        await Task.WhenAll(activateTask, closeTask);

        await using (var verification = new DraftDbContext(options))
        {
            var cycle = await verification.MarketCycles.AsNoTracking().SingleAsync(c => c.CycleId == cycleId);
            Assert.Equal(MarketCycleStatus.Closed, cycle.Status);
        }
    }

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
}
