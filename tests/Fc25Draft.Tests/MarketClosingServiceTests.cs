using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fc25Draft.Tests;

public class MarketClosingServiceTests
{
    [Fact]
    public async Task CloseItemAsync_SellsToHighestEligibleBidder()
    {
        var now = DateTimeOffset.UtcNow;
        using var context = CreateDbContext();
        SeedPositions(context);

        var teamA = CreateTeam("Team A");
        var teamB = CreateTeam("Team B");
        context.Teams.AddRange(teamA, teamB);

        context.TeamBudgets.AddRange(
            new TeamBudget { TeamId = teamA.TeamId, Saldo = 200m },
            new TeamBudget { TeamId = teamB.TeamId, Saldo = 150m });

        var player = CreatePlayer(1, "Player 1");
        context.Players.Add(player);

        var itemId = Guid.NewGuid();
        var item = new TransferMarketItem
        {
            MarketItemId = itemId,
            PlayerId = player.PlayerId,
            Player = player,
            PrecoBase = 10m,
            PrecoComprarAgora = 25m,
            DataInicioUtc = now.UtcDateTime.AddHours(-2),
            Status = "OPEN",
            LanceAtual = 60m,
            MaiorLanceTeamId = teamB.TeamId
        };

        context.TransferMarketItems.Add(item);

        context.Bids.AddRange(
            new Bid
            {
                BidId = Guid.NewGuid(),
                MarketItemId = itemId,
                TeamId = teamB.TeamId,
                Valor = 60m,
                DataUtc = now.UtcDateTime.AddMinutes(-10)
            },
            new Bid
            {
                BidId = Guid.NewGuid(),
                MarketItemId = itemId,
                TeamId = teamA.TeamId,
                Valor = 55m,
                DataUtc = now.UtcDateTime.AddMinutes(-20)
            });

        await context.SaveChangesAsync();

        var service = new MarketClosingService(context, NullLogger<MarketClosingService>.Instance, new FixedTimeProvider(now));

        var result = await service.CloseItemAsync(itemId, CancellationToken.None);

        Assert.Equal("SOLD", result.StatusAfter);
        Assert.Equal(60m, result.WinnerBidValue);
        Assert.Equal(teamB.TeamName, result.WinnerTeamName);

        var updatedItem = await context.TransferMarketItems.AsNoTracking().SingleAsync(i => i.MarketItemId == itemId);
        Assert.Equal("SOLD", updatedItem.Status);
        Assert.Equal(teamB.TeamId, updatedItem.VencedorTeamId);

        var budgetB = await context.TeamBudgets.AsNoTracking().SingleAsync(b => b.TeamId == teamB.TeamId);
        Assert.Equal(90m, budgetB.Saldo);

        var rosterEntry = await context.TeamRosters.AsNoTracking().SingleAsync(r => r.TeamId == teamB.TeamId && r.PlayerId == player.PlayerId);
        Assert.NotNull(rosterEntry);

        var history = await context.TransferHistories.AsNoTracking().SingleAsync();
        Assert.Equal(player.PlayerId, history.PlayerId);
        Assert.Equal(teamB.TeamId, history.DestinoTeamId);
        Assert.Equal(60m, history.Valor);
        Assert.Equal("MARKET_AUCTION", history.Tipo);
    }

    [Fact]
    public async Task CloseItemAsync_SkipsLeaderWithInsufficientFunds()
    {
        var now = DateTimeOffset.UtcNow;
        using var context = CreateDbContext();
        SeedPositions(context);

        var leaderTeam = CreateTeam("Leader");
        var challengerTeam = CreateTeam("Challenger");
        context.Teams.AddRange(leaderTeam, challengerTeam);

        context.TeamBudgets.AddRange(
            new TeamBudget { TeamId = leaderTeam.TeamId, Saldo = 40m },
            new TeamBudget { TeamId = challengerTeam.TeamId, Saldo = 120m });

        var player = CreatePlayer(2, "Player 2");
        context.Players.Add(player);

        var itemId = Guid.NewGuid();
        var item = new TransferMarketItem
        {
            MarketItemId = itemId,
            PlayerId = player.PlayerId,
            Player = player,
            PrecoBase = 10m,
            PrecoComprarAgora = 30m,
            DataInicioUtc = now.UtcDateTime.AddHours(-1),
            Status = "OPEN",
            LanceAtual = 50m,
            MaiorLanceTeamId = leaderTeam.TeamId
        };

        context.TransferMarketItems.Add(item);

        context.Bids.AddRange(
            new Bid
            {
                BidId = Guid.NewGuid(),
                MarketItemId = itemId,
                TeamId = leaderTeam.TeamId,
                Valor = 50m,
                DataUtc = now.UtcDateTime.AddMinutes(-5)
            },
            new Bid
            {
                BidId = Guid.NewGuid(),
                MarketItemId = itemId,
                TeamId = challengerTeam.TeamId,
                Valor = 45m,
                DataUtc = now.UtcDateTime.AddMinutes(-15)
            });

        await context.SaveChangesAsync();

        var service = new MarketClosingService(context, NullLogger<MarketClosingService>.Instance, new FixedTimeProvider(now));

        var result = await service.CloseItemAsync(itemId, CancellationToken.None);

        Assert.Equal("SOLD", result.StatusAfter);
        Assert.Equal(challengerTeam.TeamName, result.WinnerTeamName);
        Assert.Equal(45m, result.WinnerBidValue);

        var updatedItem = await context.TransferMarketItems.AsNoTracking().SingleAsync(i => i.MarketItemId == itemId);
        Assert.Equal("SOLD", updatedItem.Status);
        Assert.Equal(challengerTeam.TeamId, updatedItem.VencedorTeamId);

        var challengerBudget = await context.TeamBudgets.AsNoTracking().SingleAsync(b => b.TeamId == challengerTeam.TeamId);
        Assert.Equal(75m, challengerBudget.Saldo);

        var leaderBudget = await context.TeamBudgets.AsNoTracking().SingleAsync(b => b.TeamId == leaderTeam.TeamId);
        Assert.Equal(40m, leaderBudget.Saldo);
    }

    [Fact]
    public async Task CloseItemAsync_ExpiresWithoutBids()
    {
        var now = DateTimeOffset.UtcNow;
        using var context = CreateDbContext();
        SeedPositions(context);

        var team = CreateTeam("Team");
        context.Teams.Add(team);
        context.TeamBudgets.Add(new TeamBudget { TeamId = team.TeamId, Saldo = 100m });

        var player = CreatePlayer(3, "Player 3");
        context.Players.Add(player);

        var itemId = Guid.NewGuid();
        context.TransferMarketItems.Add(new TransferMarketItem
        {
            MarketItemId = itemId,
            PlayerId = player.PlayerId,
            Player = player,
            PrecoBase = 10m,
            PrecoComprarAgora = 30m,
            DataInicioUtc = now.UtcDateTime.AddHours(-3),
            Status = "OPEN"
        });

        await context.SaveChangesAsync();

        var service = new MarketClosingService(context, NullLogger<MarketClosingService>.Instance, new FixedTimeProvider(now));

        var result = await service.CloseItemAsync(itemId, CancellationToken.None);

        Assert.Equal("EXPIRED", result.StatusAfter);
        Assert.Null(result.WinnerTeamName);
        Assert.Null(result.WinnerBidValue);

        var updatedItem = await context.TransferMarketItems.AsNoTracking().SingleAsync(i => i.MarketItemId == itemId);
        Assert.Equal("EXPIRED", updatedItem.Status);
    }

    [Fact]
    public async Task CloseItemAsync_ExpiresWhenPlayerAlreadyInRoster()
    {
        var now = DateTimeOffset.UtcNow;
        using var context = CreateDbContext();
        SeedPositions(context);

        var team = CreateTeam("Roster Owner");
        var bidder = CreateTeam("Bidder");
        context.Teams.AddRange(team, bidder);

        context.TeamBudgets.AddRange(
            new TeamBudget { TeamId = team.TeamId, Saldo = 100m },
            new TeamBudget { TeamId = bidder.TeamId, Saldo = 100m });

        var player = CreatePlayer(4, "Player 4");
        context.Players.Add(player);
        context.TeamRosters.Add(new TeamRoster { TeamId = team.TeamId, PlayerId = player.PlayerId });

        var itemId = Guid.NewGuid();
        var item = new TransferMarketItem
        {
            MarketItemId = itemId,
            PlayerId = player.PlayerId,
            Player = player,
            PrecoBase = 10m,
            PrecoComprarAgora = 25m,
            DataInicioUtc = now.UtcDateTime.AddHours(-2),
            Status = "OPEN",
            LanceAtual = 40m,
            MaiorLanceTeamId = bidder.TeamId
        };

        context.TransferMarketItems.Add(item);
        context.Bids.Add(new Bid
        {
            BidId = Guid.NewGuid(),
            MarketItemId = itemId,
            TeamId = bidder.TeamId,
            Valor = 40m,
            DataUtc = now.UtcDateTime.AddMinutes(-30)
        });

        await context.SaveChangesAsync();

        var service = new MarketClosingService(context, NullLogger<MarketClosingService>.Instance, new FixedTimeProvider(now));

        var result = await service.CloseItemAsync(itemId, CancellationToken.None);

        Assert.Equal("EXPIRED", result.StatusAfter);
        Assert.Null(result.WinnerTeamName);

        var updatedItem = await context.TransferMarketItems.AsNoTracking().SingleAsync(i => i.MarketItemId == itemId);
        Assert.Equal("EXPIRED", updatedItem.Status);

        Assert.Equal(0, await context.TransferHistories.CountAsync());
        Assert.Equal(1, await context.TeamRosters.CountAsync());
    }

    [Fact]
    public async Task PreviewCloseAsync_ShowsEligibleAndInsufficient()
    {
        var now = DateTimeOffset.UtcNow;
        using var context = CreateDbContext();
        SeedPositions(context);

        var leader = CreateTeam("Leader");
        var challenger = CreateTeam("Challenger");
        context.Teams.AddRange(leader, challenger);

        context.TeamBudgets.AddRange(
            new TeamBudget { TeamId = leader.TeamId, Saldo = 30m },
            new TeamBudget { TeamId = challenger.TeamId, Saldo = 90m });

        var player = CreatePlayer(5, "Player 5");
        context.Players.Add(player);

        var itemId = Guid.NewGuid();
        context.TransferMarketItems.Add(new TransferMarketItem
        {
            MarketItemId = itemId,
            PlayerId = player.PlayerId,
            Player = player,
            PrecoBase = 10m,
            PrecoComprarAgora = 30m,
            DataInicioUtc = now.UtcDateTime.AddMinutes(-50),
            Status = "OPEN",
            LanceAtual = 40m,
            MaiorLanceTeamId = leader.TeamId
        });

        context.Bids.AddRange(
            new Bid
            {
                BidId = Guid.NewGuid(),
                MarketItemId = itemId,
                TeamId = leader.TeamId,
                Valor = 40m,
                DataUtc = now.UtcDateTime.AddMinutes(-45)
            },
            new Bid
            {
                BidId = Guid.NewGuid(),
                MarketItemId = itemId,
                TeamId = challenger.TeamId,
                Valor = 35m,
                DataUtc = now.UtcDateTime.AddMinutes(-40)
            });

        await context.SaveChangesAsync();

        var service = new MarketClosingService(context, NullLogger<MarketClosingService>.Instance, new FixedTimeProvider(now));

        MarketClosePreviewDto preview = await service.PreviewCloseAsync(CancellationToken.None);

        Assert.Equal(1, preview.OpenItems);
        var itemPreview = Assert.Single(preview.Items);
        Assert.Equal(itemId, itemPreview.MarketItemId);
        Assert.Equal(40m, itemPreview.HighestBid);
        Assert.Equal("Leader", itemPreview.HighestBidTeam);
        Assert.True(itemPreview.HasEligibleWinner);
        Assert.StartsWith("SELL to", itemPreview.Decision);
    }

    private static DraftDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DraftDbContext(options);
    }

    private static void SeedPositions(DraftDbContext context)
    {
        context.Positions.Add(new Position { PositionId = 1, Name = "Pos" });
        context.SaveChanges();
    }

    private static Player CreatePlayer(int id, string name)
    {
        return new Player
        {
            PlayerId = id,
            Name = name,
            Age = 25,
            Overall = 80,
            PositionId = 1
        };
    }

    private static Team CreateTeam(string name)
    {
        return new Team
        {
            TeamId = Guid.NewGuid(),
            TeamName = name,
            TeamToken = Guid.NewGuid()
        };
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
