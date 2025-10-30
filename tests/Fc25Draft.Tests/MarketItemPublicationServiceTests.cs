using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Tests;

public class MarketItemPublicationServiceTests
{
    [Fact]
    public async Task CreateDraftAsync_ThrowsValidation_WhenBuyNowNotGreaterThanBase()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));
        await using var context = CreateDbContext();
        await SeedCycleAndPlayerAsync(context, cycleId: out var cycleId, playerId: out var playerId);

        var service = new MarketItemPublicationService(context, fakeTime);
        var expiresAt = fakeTime.GetUtcNow().UtcDateTime.AddHours(4);
        var request = new MarketItemDraftCreateRequest(cycleId, playerId, 1_000m, 900m, 100m, expiresAt);

        var exception = await Assert.ThrowsAsync<MarketItemValidationException>(
            () => service.CreateDraftAsync(request, CancellationToken.None));

        Assert.Contains("buyNowPrice", exception.Errors.Keys);
    }

    [Fact]
    public async Task UpdateDraftAsync_ThrowsValidation_WhenExpirationIsNotFuture()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 2, 1, 8, 0, 0, TimeSpan.Zero));
        await using var context = CreateDbContext();
        await SeedCycleAndPlayerAsync(context, cycleId: out var cycleId, playerId: out var playerId);

        var item = new MarketItem
        {
            ItemId = Guid.NewGuid(),
            CycleId = cycleId,
            PlayerId = playerId,
            BasePrice = 5_000m,
            BuyNowPrice = 6_000m,
            MinIncrement = 200m,
            ExpiresAtUtc = fakeTime.GetUtcNow().UtcDateTime.AddHours(2),
            Status = MarketItemStatus.Draft,
            CreatedAtUtc = fakeTime.GetUtcNow().UtcDateTime,
            LastUpdateUtc = fakeTime.GetUtcNow().UtcDateTime,
            RowVersion = 7
        };

        context.MarketItems.Add(item);
        await context.SaveChangesAsync();

        var service = new MarketItemPublicationService(context, fakeTime);
        var request = new MarketItemDraftUpdateRequest(5_500m, 6_500m, 200m, fakeTime.GetUtcNow().UtcDateTime.AddMinutes(-5));

        var exception = await Assert.ThrowsAsync<MarketItemValidationException>(
            () => service.UpdateDraftAsync(item.ItemId, request, item.RowVersion, CancellationToken.None));

        Assert.Contains("expiresAtUtc", exception.Errors.Keys);
    }

    [Fact]
    public async Task UpdateDraftAsync_ThrowsConflict_WhenItemIsNotDraft()
    {
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2024, 3, 5, 15, 30, 0, TimeSpan.Zero));
        await using var context = CreateDbContext();
        await SeedCycleAndPlayerAsync(context, cycleId: out var cycleId, playerId: out var playerId);

        var item = new MarketItem
        {
            ItemId = Guid.NewGuid(),
            CycleId = cycleId,
            PlayerId = playerId,
            BasePrice = 4_500m,
            BuyNowPrice = 5_200m,
            MinIncrement = 150m,
            ExpiresAtUtc = fakeTime.GetUtcNow().UtcDateTime.AddHours(6),
            Status = MarketItemStatus.Published,
            CreatedAtUtc = fakeTime.GetUtcNow().UtcDateTime,
            LastUpdateUtc = fakeTime.GetUtcNow().UtcDateTime,
            RowVersion = 11
        };

        context.MarketItems.Add(item);
        await context.SaveChangesAsync();

        var service = new MarketItemPublicationService(context, fakeTime);
        var request = new MarketItemDraftUpdateRequest(4_800m, 5_500m, 200m, fakeTime.GetUtcNow().UtcDateTime.AddHours(1));

        await Assert.ThrowsAsync<MarketConflictException>(
            () => service.UpdateDraftAsync(item.ItemId, request, item.RowVersion, CancellationToken.None));
    }

    private static DraftDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new DraftDbContext(options);
    }

    private static async Task SeedCycleAndPlayerAsync(
        DraftDbContext context,
        out Guid cycleId,
        out int playerId)
    {
        await context.Database.EnsureCreatedAsync();

        cycleId = Guid.NewGuid();
        playerId = Random.Shared.Next(1_000, 9_999);
        var now = DateTime.UtcNow;

        var position = new Position
        {
            PositionId = 99,
            Name = "Test Position"
        };

        context.Positions.Add(position);

        context.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            CreatedAtUtc = now,
            NextCycleAtUtc = now.AddDays(7),
            Status = MarketCycleStatus.Active
        });

        context.Players.Add(new Player
        {
            PlayerId = playerId,
            Name = "Test Player",
            Overall = 85,
            PositionId = position.PositionId,
            Position = position,
            PlayerGuid = Guid.NewGuid()
        });

        await context.SaveChangesAsync();
    }
}
