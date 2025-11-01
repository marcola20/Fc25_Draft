using System;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fc25Draft.Tests;

public class AuctionSettlementServiceTests
{
    [Fact]
    public async Task SettleExpiredItemsAsync_SellsWinningBid()
    {
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var position = new Position { PositionId = 1, Name = "Goleiro" };
        var teamId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var player = new Player
        {
            PlayerId = 10,
            Name = "Jogador Teste",
            PositionId = position.PositionId,
            Overall = 90,
            PlayerGuid = Guid.NewGuid()
        };

        var team = new Team
        {
            TeamId = teamId,
            TeamName = "Equipe Vencedora",
            Token = "TOKEN-TESTE",
            Budget = 5000m,
            BudgetBlocked = 2000m
        };

        var cycle = new MarketCycle
        {
            CycleId = cycleId,
            Name = "Ciclo Teste",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = now.AddHours(-2),
            EndsAtUtc = now.AddHours(5),
            CreatedAtUtc = now.AddHours(-3),
            UpdatedAtUtc = now.AddHours(-1)
        };

        var item = new MarketItem
        {
            ItemId = itemId,
            CycleId = cycleId,
            PlayerId = player.PlayerId,
            BasePrice = 1000m,
            MinIncrement = 100m,
            ExpiresAtUtc = now.AddMinutes(-10),
            Status = MarketItemStatus.Active,
            CreatedAtUtc = now.AddHours(-4),
            LastUpdateUtc = now.AddHours(-2),
            CurrentLeaderTeamId = teamId,
            CurrentLeaderAmount = 2000m
        };

        context.Positions.Add(position);
        context.Teams.Add(team);
        context.Players.Add(player);
        context.MarketCycles.Add(cycle);
        context.MarketItems.Add(item);
        await context.SaveChangesAsync();

        var logService = new TransactionLogService(context);
        var service = new AuctionSettlementService(context, logService, NullLogger<AuctionSettlementService>.Instance);

        var summary = await service.SettleExpiredItemsAsync(cycleId, CancellationToken.None);

        Assert.Equal(new AuctionSettlementResult(1, 0), summary);

        var updatedItem = await context.MarketItems.AsNoTracking().FirstAsync(i => i.ItemId == itemId);
        Assert.Equal(MarketItemStatus.Sold, updatedItem.Status);
        Assert.Equal(teamId, updatedItem.WinnerTeamId);
        Assert.Equal(2000m, updatedItem.CurrentLeaderAmount);

        var updatedTeam = await context.Teams.AsNoTracking().FirstAsync(t => t.TeamId == teamId);
        Assert.Equal(3000m, updatedTeam.Budget);
        Assert.Equal(0m, updatedTeam.BudgetBlocked);

        var rosterEntry = await context.TeamRosters.AsNoTracking().FirstOrDefaultAsync(r => r.TeamId == teamId && r.PlayerId == player.PlayerId);
        Assert.NotNull(rosterEntry);

        var historyEntry = await context.TransferHistories.AsNoTracking().SingleAsync();
        Assert.Equal(player.PlayerId, historyEntry.PlayerId);
        Assert.Equal(teamId, historyEntry.ToTeamId);
        Assert.Equal(2000m, historyEntry.Amount);
        Assert.Equal(TransferType.MarketAuction, historyEntry.Type);

        var transaction = await context.MarketTransactions.AsNoTracking().SingleAsync();
        Assert.Equal(MarketTransactionType.AuctionSettled, transaction.Type);
        Assert.Equal(teamId, transaction.TeamId);
        Assert.Equal(2000m, transaction.Amount);
    }

    [Fact]
    public async Task SettleExpiredItemsAsync_ExpiresWhenNoLeader()
    {
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var position = new Position { PositionId = 2, Name = "Zagueiro" };
        var cycleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var player = new Player
        {
            PlayerId = 20,
            Name = "Sem Lance",
            PositionId = position.PositionId,
            Overall = 80,
            PlayerGuid = Guid.NewGuid()
        };

        var cycle = new MarketCycle
        {
            CycleId = cycleId,
            Name = "Ciclo Sem Lances",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = now.AddHours(-2),
            EndsAtUtc = now.AddHours(3),
            CreatedAtUtc = now.AddHours(-4),
            UpdatedAtUtc = now.AddHours(-1)
        };

        var item = new MarketItem
        {
            ItemId = itemId,
            CycleId = cycleId,
            PlayerId = player.PlayerId,
            BasePrice = 500m,
            MinIncrement = 50m,
            ExpiresAtUtc = now.AddMinutes(-5),
            Status = MarketItemStatus.Active,
            CreatedAtUtc = now.AddHours(-5),
            LastUpdateUtc = now.AddHours(-2)
        };

        context.Positions.Add(position);
        context.Players.Add(player);
        context.MarketCycles.Add(cycle);
        context.MarketItems.Add(item);
        await context.SaveChangesAsync();

        var logService = new TransactionLogService(context);
        var service = new AuctionSettlementService(context, logService, NullLogger<AuctionSettlementService>.Instance);

        var summary = await service.SettleExpiredItemsAsync(cycleId, CancellationToken.None);

        Assert.Equal(new AuctionSettlementResult(0, 1), summary);

        var updatedItem = await context.MarketItems.AsNoTracking().FirstAsync(i => i.ItemId == itemId);
        Assert.Equal(MarketItemStatus.Expired, updatedItem.Status);
        Assert.Null(updatedItem.WinnerTeamId);

        Assert.False(await context.TeamRosters.AnyAsync(r => r.PlayerId == player.PlayerId));
        Assert.False(await context.TransferHistories.AnyAsync());

        var transaction = await context.MarketTransactions.AsNoTracking().SingleAsync();
        Assert.Equal(MarketTransactionType.AuctionExpired, transaction.Type);
        Assert.Equal(itemId, transaction.ItemId);
    }

    [Fact]
    public async Task SettleAllOpenItemsOnCycleCloseAsync_ProcessesActiveItems()
    {
        using var context = CreateContext();
        var now = DateTime.UtcNow;

        var position = new Position { PositionId = 3, Name = "Meia" };
        var cycleId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        var player = new Player
        {
            PlayerId = 30,
            Name = "Jogador Futuro",
            PositionId = position.PositionId,
            Overall = 88,
            PlayerGuid = Guid.NewGuid()
        };

        var team = new Team
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Equipe Futuro",
            Token = "TOKEN-FUTURO",
            Budget = 7000m,
            BudgetBlocked = 1500m
        };

        var cycle = new MarketCycle
        {
            CycleId = cycleId,
            Name = "Ciclo Encerrado",
            Status = MarketCycleStatus.Active,
            StartsAtUtc = now.AddHours(-1),
            EndsAtUtc = now.AddHours(2),
            CreatedAtUtc = now.AddHours(-3),
            UpdatedAtUtc = now.AddHours(-1)
        };

        var item = new MarketItem
        {
            ItemId = itemId,
            CycleId = cycleId,
            PlayerId = player.PlayerId,
            BasePrice = 800m,
            MinIncrement = 80m,
            ExpiresAtUtc = now.AddHours(1),
            Status = MarketItemStatus.Active,
            CreatedAtUtc = now.AddHours(-2),
            LastUpdateUtc = now.AddHours(-1),
            CurrentLeaderTeamId = team.TeamId,
            CurrentLeaderAmount = 1500m
        };

        context.Positions.Add(position);
        context.Teams.Add(team);
        context.Players.Add(player);
        context.MarketCycles.Add(cycle);
        context.MarketItems.Add(item);
        await context.SaveChangesAsync();

        var logService = new TransactionLogService(context);
        var service = new AuctionSettlementService(context, logService, NullLogger<AuctionSettlementService>.Instance);

        var summary = await service.SettleAllOpenItemsOnCycleCloseAsync(cycleId, CancellationToken.None);

        Assert.Equal(new AuctionSettlementResult(1, 0), summary);

        var updatedItem = await context.MarketItems.AsNoTracking().FirstAsync(i => i.ItemId == itemId);
        Assert.Equal(MarketItemStatus.Sold, updatedItem.Status);
        Assert.Equal(team.TeamId, updatedItem.WinnerTeamId);

        var updatedTeam = await context.Teams.AsNoTracking().FirstAsync(t => t.TeamId == team.TeamId);
        Assert.Equal(5500m, updatedTeam.Budget);
        Assert.Equal(0m, updatedTeam.BudgetBlocked);

        var rosterEntry = await context.TeamRosters.AsNoTracking().FirstOrDefaultAsync(r => r.TeamId == team.TeamId && r.PlayerId == player.PlayerId);
        Assert.NotNull(rosterEntry);
    }

    private static DraftDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase($"auction-settlement-{Guid.NewGuid():N}")
            .Options;
        return new DraftDbContext(options);
    }
}
