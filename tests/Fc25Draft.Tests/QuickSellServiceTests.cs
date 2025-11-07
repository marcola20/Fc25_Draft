using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fc25Draft.Tests;

public class QuickSellServiceTests
{
    [Fact]
    public async Task QuickSellAsync_RemovesPlayerAndCreditsBudget()
    {
        await using var context = CreateContext();
        var teamId = Guid.NewGuid();
        var targetPlayerId = 1;
        var pricing = new PricingResult(100_000_000m, 0m, 0m);
        var pricingService = new StubPricingService(pricing);
        var service = new QuickSellService(context, pricingService, TimeProvider.System);

        SeedTeamWithPlayers(context, teamId, targetPlayerId, rosterSize: 19);
        await context.SaveChangesAsync();

        var result = await service.QuickSellAsync(teamId, targetPlayerId, "token", CancellationToken.None);

        var payout = decimal.Round(pricing.BasePrice * 0.8m, 2, MidpointRounding.AwayFromZero);
        var team = await context.Teams.AsNoTracking().SingleAsync(t => t.TeamId == teamId);
        Assert.Equal(decimal.Round(200_000_000m + payout, 2, MidpointRounding.AwayFromZero), team.Budget);

        var player = await context.Players.SingleAsync(p => p.PlayerId == targetPlayerId);
        Assert.Null(player.CurrentTeamId);
        Assert.Equal(PlayerStatus.FreeAgent, player.Status);
        Assert.Equal(player.PreviousOverall, result.OldOverall);
        Assert.Equal(player.Overall, result.NewOverall);

        Assert.False(await context.TeamRosters.AnyAsync(r => r.TeamId == teamId && r.PlayerId == targetPlayerId));

        var history = await context.TransferHistories.SingleAsync();
        Assert.Equal(TransferType.QuickSell, history.Type);
        Assert.Equal(payout, history.Amount);
        Assert.Equal(payout, history.Payout);
        Assert.Equal(result.OldOverall, history.OldOverall);
        Assert.Equal(result.NewOverall, history.NewOverall);
        Assert.Equal(result.OccurredAtUtc, history.OccurredAtUtc);

        var ledger = await context.BudgetLedgers.SingleAsync();
        Assert.Equal("QUICKSELL", ledger.Origem);
        Assert.Equal("CREDIT", ledger.Tipo);
        Assert.Equal(payout, ledger.Valor);

        Assert.Equal(payout, result.Payout);
        Assert.Equal(pricing.BasePrice, result.BasePrice);
    }

    [Fact]
    public async Task QuickSellAsync_Throws_WhenRosterWouldDropBelowMinimum()
    {
        await using var context = CreateContext();
        var teamId = Guid.NewGuid();
        var pricing = new PricingResult(50_000_000m, 0m, 0m);
        var pricingService = new StubPricingService(pricing);
        var service = new QuickSellService(context, pricingService, TimeProvider.System);

        SeedTeamWithPlayers(context, teamId, targetPlayerId: 1, rosterSize: 18);
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.QuickSellAsync(teamId, 1, "token", CancellationToken.None));
    }

    [Fact]
    public void CalculateOverallBump_RespectsUpperCap()
    {
        var player = new Player
        {
            PlayerId = 99,
            PlayerGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Name = "Test",
            Overall = 98,
            PositionId = 1,
            Status = PlayerStatus.Active
        };

        var bumped = QuickSellService.CalculateOverallBump(player);
        Assert.InRange(bumped, 99, 99);
    }

    private static DraftDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DraftDbContext(options);
    }

    private static void SeedTeamWithPlayers(DraftDbContext context, Guid teamId, int targetPlayerId, int rosterSize)
    {
        if (!context.Positions.Any())
        {
            context.Positions.Add(new Position { PositionId = 1, Name = "Goleiro" });
        }

        var token = "TOKEN";
        var team = new Team
        {
            TeamId = teamId,
            TeamName = "Time Teste",
            Token = token,
            OwnerName = "Coach",
            Budget = 200_000_000m,
            BudgetBlocked = 0m
        };

        context.Teams.Add(team);

        for (var i = 0; i < rosterSize; i++)
        {
            var playerId = i + 1;
            var player = new Player
            {
                PlayerId = playerId,
                PlayerGuid = Guid.NewGuid(),
                Name = $"Jogador {playerId}",
                Age = 25,
                Overall = 70 + i % 5,
                PreviousOverall = null,
                Status = PlayerStatus.Active,
                PositionId = 1,
                CurrentTeamId = teamId
            };

            if (playerId == targetPlayerId)
            {
                player.Overall = 80;
            }

            context.Players.Add(player);
            context.TeamRosters.Add(new TeamRoster { TeamId = teamId, PlayerId = playerId, Team = team, Player = player });
        }
    }

    private sealed class StubPricingService : IPricingService
    {
        private readonly PricingResult _result;

        public StubPricingService(PricingResult result) => _result = result;

        public PricingResult Calculate(decimal positionWeight, int overall, int age) => _result;

        public Task<PricingResult> CalculateForPositionAsync(string? positionCode, short? positionId, int age, int overall, CancellationToken ct)
            => Task.FromResult(_result);

        public Task<PricingResult> CalculateForPlayerAsync(int playerId, CancellationToken ct)
            => Task.FromResult(_result);

        public decimal RoundUp(decimal value, decimal step) => value;

        public decimal Round(decimal value, decimal step) => value;
    }
}
