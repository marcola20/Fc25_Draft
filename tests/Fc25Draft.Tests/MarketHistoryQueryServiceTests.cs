using System;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fc25Draft.Tests;

public class MarketHistoryQueryServiceTests
{
    [Fact]
    public async Task QueryAsync_ReturnsUtcTimestamps()
    {
        await using var context = CreateDbContext();

        var now = DateTime.UtcNow;
        var cycleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        const int playerId = 500;

        context.Positions.Add(new Position { PositionId = 1, Name = "Atacante" });
        context.Teams.Add(new Team { TeamId = teamId, TeamName = "Time Alpha", Token = "TOKEN", Budget = 1_000m, BudgetBlocked = 0m });
        context.Players.Add(new Player
        {
            PlayerId = playerId,
            PlayerGuid = Guid.NewGuid(),
            Name = "Jogador Histórico",
            Overall = 88,
            PositionId = 1
        });
        context.MarketCycles.Add(new MarketCycle
        {
            CycleId = cycleId,
            Name = "Ciclo Histórico",
            Status = MarketCycleStatus.Closed,
            StartsAtUtc = now.AddHours(-4),
            EndsAtUtc = now.AddHours(4),
            CreatedAtUtc = now.AddHours(-5),
            UpdatedAtUtc = now
        });

        context.MarketTransactions.Add(new MarketTransaction
        {
            TransactionId = Guid.NewGuid(),
            CycleId = cycleId,
            ItemId = itemId,
            PlayerId = playerId,
            TeamId = teamId,
            TargetTeamId = null,
            Type = MarketTransactionType.AuctionSettled,
            Amount = 750m,
            PerformedBy = "tester",
            Notes = "registro",
            CreatedAtUtc = now
        });

        await context.SaveChangesAsync();

        var service = new MarketHistoryQueryService(context);
        var filter = new MarketHistoryFilter { Page = 1, PageSize = 10 };

        var result = await service.QueryAsync(filter, CancellationToken.None);
        var entry = Assert.Single(result.Items);
        Assert.Equal(now, entry.OccurredAtUtc);
        Assert.Equal(DateTimeKind.Utc, entry.OccurredAtUtc.Kind);
    }

    private static DraftDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase($"market-history-{Guid.NewGuid():N}")
            .Options;

        var context = new DraftDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
