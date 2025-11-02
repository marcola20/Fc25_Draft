using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Fc25Draft.Web.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Fc25Draft.Tests;

public class MarketServiceTests
{
    [Fact]
    public async Task PlaceBidAsync_StoresUtcTimestamps()
    {
        var fakeNow = new DateTimeOffset(2024, 7, 1, 12, 30, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(fakeNow);

        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseSqlite(connection)
            .Options;

        var cycleId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        const int playerId = 1234;

        await using (var setupContext = new DraftDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();

            setupContext.Positions.Add(new Position { PositionId = 1, Name = "Atacante" });

            setupContext.Players.Add(new Player
            {
                PlayerId = playerId,
                PlayerGuid = Guid.NewGuid(),
                Name = "Jogador Teste",
                Overall = 85,
                PositionId = 1
            });

            setupContext.Teams.Add(new Team
            {
                TeamId = teamId,
                TeamName = "Time Teste",
                Token = "TEAMTOKEN",
                Budget = 2_000_000m,
                BudgetBlocked = 0m
            });

            setupContext.MarketCycles.Add(new MarketCycle
            {
                CycleId = cycleId,
                Name = "Ciclo Teste",
                Status = MarketCycleStatus.Active,
                StartsAtUtc = fakeNow.UtcDateTime.AddDays(-1),
                EndsAtUtc = fakeNow.UtcDateTime.AddDays(1),
                CreatedAtUtc = fakeNow.UtcDateTime.AddDays(-1),
                UpdatedAtUtc = fakeNow.UtcDateTime.AddDays(-1)
            });

            setupContext.MarketItems.Add(new MarketItem
            {
                ItemId = itemId,
                CycleId = cycleId,
                PlayerId = playerId,
                BasePrice = 50_000m,
                BuyNowPrice = 100_000m,
                MinIncrement = 5_000m,
                ExpiresAtUtc = fakeNow.UtcDateTime.AddHours(2),
                Status = MarketItemStatus.Active,
                CreatedAtUtc = fakeNow.UtcDateTime.AddHours(-5),
                LastUpdateUtc = fakeNow.UtcDateTime.AddHours(-5),
                RowVersion = 1
            });

            await setupContext.SaveChangesAsync();
        }

        var log = new RecordingTransactionLogService();
        var optionsWrapper = Options.Create(new MarketOptions());
        var budget = new FixedBudgetService(available: 1_000_000m);

        await using var context = new DraftDbContext(options);
        var service = new MarketService(
            context,
            new NoopMarketCycleGenerator(),
            optionsWrapper,
            log,
            budget,
            fakeTime);

        await service.PlaceBidAsync(itemId, "teamtoken", 60_000m, 1, CancellationToken.None);

        var bid = await context.MarketBids.AsNoTracking().SingleAsync();
        var item = await context.MarketItems.AsNoTracking().SingleAsync(i => i.ItemId == itemId);

        Assert.Equal(DateTimeKind.Utc, bid.CreatedAtUtc.Kind);
        Assert.Equal(DateTimeKind.Utc, item.LastUpdateUtc.Kind);
        Assert.Equal(fakeNow.UtcDateTime, bid.CreatedAtUtc);
        Assert.Equal(fakeNow.UtcDateTime, item.LastUpdateUtc);

        var entry = Assert.Single(log.Entries);
        Assert.Equal(DateTimeKind.Utc, entry.OccurredAtUtc.Kind);
        Assert.Equal(fakeNow.UtcDateTime, entry.OccurredAtUtc);
    }

    [Fact]
    public async Task PlaceBidAsync_ReleasesBudgetBlockedForPreviousLeader()
    {
        var fakeNow = new DateTimeOffset(2024, 8, 10, 14, 0, 0, TimeSpan.Zero);
        var fakeTime = new FakeTimeProvider(fakeNow);

        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseSqlite(connection)
            .Options;

        var cycleId = Guid.NewGuid();
        var teamA = Guid.NewGuid();
        var teamB = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        const int playerId = 4321;

        await using (var setup = new DraftDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();

            setup.Positions.Add(new Position { PositionId = 1, Name = "Meia" });
            setup.Players.Add(new Player
            {
                PlayerId = playerId,
                PlayerGuid = Guid.NewGuid(),
                Name = "Craque",
                Overall = 89,
                PositionId = 1
            });

            setup.Teams.AddRange(
                new Team
                {
                    TeamId = teamA,
                    TeamName = "Time A",
                    Token = "TOKEN-A",
                    Budget = 1_000_000m,
                    BudgetBlocked = 0m
                },
                new Team
                {
                    TeamId = teamB,
                    TeamName = "Time B",
                    Token = "TOKEN-B",
                    Budget = 1_000_000m,
                    BudgetBlocked = 0m
                });

            setup.MarketCycles.Add(new MarketCycle
            {
                CycleId = cycleId,
                Name = "Ciclo",
                Status = MarketCycleStatus.Active,
                StartsAtUtc = fakeNow.UtcDateTime.AddHours(-2),
                EndsAtUtc = fakeNow.UtcDateTime.AddHours(2),
                CreatedAtUtc = fakeNow.UtcDateTime.AddHours(-3),
                UpdatedAtUtc = fakeNow.UtcDateTime.AddHours(-3)
            });

            setup.MarketItems.Add(new MarketItem
            {
                ItemId = itemId,
                CycleId = cycleId,
                PlayerId = playerId,
                BasePrice = 50_000m,
                MinIncrement = 5_000m,
                ExpiresAtUtc = fakeNow.UtcDateTime.AddHours(1),
                Status = MarketItemStatus.Active,
                CreatedAtUtc = fakeNow.UtcDateTime.AddHours(-4),
                LastUpdateUtc = fakeNow.UtcDateTime.AddHours(-4),
                RowVersion = 1
            });

            await setup.SaveChangesAsync();
        }

        var log = new RecordingTransactionLogService();
        var optionsWrapper = Options.Create(new MarketOptions());
        var budget = new FixedBudgetService(available: 2_000_000m);

        await using var context = new DraftDbContext(options);
        var service = new MarketService(
            context,
            new NoopMarketCycleGenerator(),
            optionsWrapper,
            log,
            budget,
            fakeTime);

        await service.PlaceBidAsync(itemId, "TOKEN-A", 18_000m, 1, CancellationToken.None);
        var itemAfterFirst = await context.MarketItems.AsNoTracking().SingleAsync(i => i.ItemId == itemId);
        await service.PlaceBidAsync(itemId, "token-b", 19_000m, itemAfterFirst.RowVersion, CancellationToken.None);

        var teams = await context.Teams.AsNoTracking().ToDictionaryAsync(t => t.TeamId);
        Assert.Equal(0m, teams[teamA].BudgetBlocked);
        Assert.Equal(19_000m, teams[teamB].BudgetBlocked);
    }

    [Fact]
    public void BrazilTimeFormatter_HandlesDstBoundary()
    {
        var beforeDst = new DateTime(2018, 11, 4, 2, 30, 0, DateTimeKind.Utc);
        var afterDst = new DateTime(2018, 11, 4, 3, 30, 0, DateTimeKind.Utc);

        var formattedBefore = BrazilTime.FormatDateTime(beforeDst);
        var formattedAfter = BrazilTime.FormatDateTime(afterDst);

        Assert.Equal("03/11/2018 23:30", formattedBefore);
        Assert.Equal("04/11/2018 01:30", formattedAfter);
    }

    private sealed class NoopMarketCycleGenerator : IMarketCycleGenerator
    {
        public Task<MarketCycleDto> CreateNewCycleAsync(DateTime utcNow, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<bool> NeedsNewCycleAsync(DateTime utcNow, CancellationToken ct)
            => Task.FromResult(false);
    }

    private sealed class FixedBudgetService : IBudgetService
    {
        private readonly decimal _available;

        public FixedBudgetService(decimal available)
        {
            _available = available;
        }

        public Task<decimal> GetAvailableAsync(Guid teamId, Guid? excludeItemId, CancellationToken ct)
            => Task.FromResult(_available);

        public Task<decimal> GetSaldoAsync(Guid teamId, CancellationToken ct)
            => Task.FromResult(_available);

        public Task<decimal> GetBloqueadoEmLancesAsync(Guid teamId, CancellationToken ct)
            => Task.FromResult(0m);

        public Task<decimal> GetSaldoDisponivelAsync(Guid teamId, CancellationToken ct)
            => Task.FromResult(_available);

        public Task RegistrarAjusteAsync(Guid teamId, decimal valor, string origem, string? descricao, bool credito, CancellationToken ct)
            => Task.CompletedTask;

        public decimal CalculateMatchRewardAmount(MatchRewardRequestDto request)
            => 0m;

        public Task<MatchRewardResult> ApplyMatchRewardAsync(MatchRewardRequestDto request, CancellationToken ct)
            => Task.FromResult(new MatchRewardResult(request.TeamId, 0m, 0m, false, string.Empty, string.Empty));
    }

    private sealed class RecordingTransactionLogService : ITransactionLogService
    {
        public List<(MarketItem Item, MarketTransactionType Type, DateTime OccurredAtUtc)> Entries { get; } = new();

        public Task LogMarketAsync(
            MarketItem item,
            MarketTransactionType type,
            Guid? teamId,
            Guid? targetTeamId,
            decimal? amount,
            string performedBy,
            string? notes,
            DateTime occurredAtUtc,
            CancellationToken ct)
        {
            Entries.Add((item, type, occurredAtUtc));
            return Task.CompletedTask;
        }
    }
}
