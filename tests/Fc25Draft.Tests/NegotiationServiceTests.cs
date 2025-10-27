using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fc25Draft.Tests;

public class NegotiationServiceTests
{
    [Fact]
    public async Task CreateAndAcceptSaleAsync_CompletesTransferAndBudgets()
    {
        using var context = CreateDbContext();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)));

        var originToken = Guid.NewGuid();
        var destinationToken = Guid.NewGuid();

        var originTeam = new Team
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Origin",
            TeamToken = originToken
        };

        var destinationTeam = new Team
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Destination",
            TeamToken = destinationToken
        };

        var player = new Player
        {
            PlayerId = 1,
            Name = "Player",
            PositionId = 1,
            Overall = 80
        };

        context.Teams.AddRange(originTeam, destinationTeam);
        context.Players.Add(player);
        context.TeamRosters.Add(new TeamRoster { TeamId = originTeam.TeamId, PlayerId = player.PlayerId });
        context.TeamBudgets.Add(new TeamBudget { TeamId = destinationTeam.TeamId, Saldo = 500m });
        context.TeamBudgets.Add(new TeamBudget { TeamId = originTeam.TeamId, Saldo = 0m });
        await context.SaveChangesAsync();

        var service = new NegotiationService(context, timeProvider);

        var createDto = new NegotiationCreateDto(
            originToken.ToString(),
            destinationToken.ToString(),
            "SALE",
            100m,
            new[] { player.PlayerId },
            Array.Empty<int>(),
            "Oferta"
        );

        var negotiation = await service.CreateAsync(createDto, CancellationToken.None);

        Assert.Equal("PENDING", negotiation.Status);
        Assert.Equal("SALE", negotiation.Tipo);
        Assert.Equal(100m, negotiation.ValorOferecido);

        var responseDto = new NegotiationResponseDto(destinationToken.ToString(), "ACCEPT");
        var completed = await service.RespondAsync(negotiation.NegotiationId, responseDto, CancellationToken.None);

        Assert.Equal("COMPLETED", completed.Status);
        Assert.Equal(timeProvider.FixedUtcNow.UtcDateTime, completed.DataFechamentoUtc);
        Assert.False(await context.TeamRosters.AnyAsync(r => r.TeamId == originTeam.TeamId && r.PlayerId == player.PlayerId));
        Assert.True(await context.TeamRosters.AnyAsync(r => r.TeamId == destinationTeam.TeamId && r.PlayerId == player.PlayerId));

        var destinationBudget = await context.TeamBudgets.FindAsync(destinationTeam.TeamId);
        var originBudget = await context.TeamBudgets.FindAsync(originTeam.TeamId);

        Assert.Equal(400m, destinationBudget!.Saldo);
        Assert.Equal(100m, originBudget!.Saldo);

        var ledgerEntries = await context.BudgetLedgers.ToListAsync();
        Assert.Equal(2, ledgerEntries.Count);
        Assert.Contains(ledgerEntries, l => l.TeamId == destinationTeam.TeamId && l.Tipo == "DEBIT" && l.Valor == 100m);
        Assert.Contains(ledgerEntries, l => l.TeamId == originTeam.TeamId && l.Tipo == "CREDIT" && l.Valor == 100m);

        var history = await context.TransferHistories.SingleAsync();
        Assert.Equal(player.PlayerId, history.PlayerId);
        Assert.Equal(originTeam.TeamId, history.OrigemTeamId);
        Assert.Equal(destinationTeam.TeamId, history.DestinoTeamId);
        Assert.Equal("TEAM_SALE", history.Tipo);
        Assert.Equal(100m, history.Valor);
    }

    [Fact]
    public async Task CreateAndAcceptTradeAsync_SwapsPlayers()
    {
        using var context = CreateDbContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);

        var originTeam = new Team
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Origin",
            TeamToken = Guid.NewGuid()
        };

        var destinationTeam = new Team
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Destination",
            TeamToken = Guid.NewGuid()
        };

        var playerOrigin = new Player { PlayerId = 10, Name = "Origin Player", PositionId = 1, Overall = 75 };
        var playerDestination = new Player { PlayerId = 20, Name = "Destination Player", PositionId = 1, Overall = 78 };

        context.Teams.AddRange(originTeam, destinationTeam);
        context.Players.AddRange(playerOrigin, playerDestination);
        context.TeamRosters.AddRange(
            new TeamRoster { TeamId = originTeam.TeamId, PlayerId = playerOrigin.PlayerId },
            new TeamRoster { TeamId = destinationTeam.TeamId, PlayerId = playerDestination.PlayerId });
        await context.SaveChangesAsync();

        var service = new NegotiationService(context, timeProvider);

        var createDto = new NegotiationCreateDto(
            originTeam.TeamToken.ToString(),
            destinationTeam.TeamToken.ToString(),
            "TRADE",
            null,
            new[] { playerOrigin.PlayerId },
            new[] { playerDestination.PlayerId },
            null);

        var negotiation = await service.CreateAsync(createDto, CancellationToken.None);
        var responseDto = new NegotiationResponseDto(destinationTeam.TeamToken.ToString(), "ACCEPT");
        var completed = await service.RespondAsync(negotiation.NegotiationId, responseDto, CancellationToken.None);

        Assert.Equal("COMPLETED", completed.Status);
        Assert.True(await context.TeamRosters.AnyAsync(r => r.TeamId == destinationTeam.TeamId && r.PlayerId == playerOrigin.PlayerId));
        Assert.True(await context.TeamRosters.AnyAsync(r => r.TeamId == originTeam.TeamId && r.PlayerId == playerDestination.PlayerId));
        Assert.Empty(await context.BudgetLedgers.ToListAsync());
        Assert.Equal(2, await context.TransferHistories.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ThrowsForDuplicatePlayers()
    {
        using var context = CreateDbContext();
        var service = new NegotiationService(context, new FixedTimeProvider(DateTimeOffset.UtcNow));

        var originTeam = new Team { TeamId = Guid.NewGuid(), TeamName = "Origin", TeamToken = Guid.NewGuid() };
        var destinationTeam = new Team { TeamId = Guid.NewGuid(), TeamName = "Destination", TeamToken = Guid.NewGuid() };
        var player = new Player { PlayerId = 30, Name = "Duplicated", PositionId = 1, Overall = 70 };

        context.Teams.AddRange(originTeam, destinationTeam);
        context.Players.Add(player);
        context.TeamRosters.Add(new TeamRoster { TeamId = originTeam.TeamId, PlayerId = player.PlayerId });
        context.TeamBudgets.Add(new TeamBudget { TeamId = destinationTeam.TeamId, Saldo = 100m });
        await context.SaveChangesAsync();

        var dto = new NegotiationCreateDto(
            originTeam.TeamToken.ToString(),
            destinationTeam.TeamToken.ToString(),
            "SALE",
            50m,
            new[] { player.PlayerId, player.PlayerId },
            Array.Empty<int>(),
            null);

        await Assert.ThrowsAsync<NegotiationValidationException>(() => service.CreateAsync(dto, CancellationToken.None));
    }

    [Fact]
    public async Task RespondAsync_FailsWhenSaldoInsuficiente()
    {
        using var context = CreateDbContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);

        var originTeam = new Team { TeamId = Guid.NewGuid(), TeamName = "Origin", TeamToken = Guid.NewGuid() };
        var destinationTeam = new Team { TeamId = Guid.NewGuid(), TeamName = "Destination", TeamToken = Guid.NewGuid() };
        var player = new Player { PlayerId = 40, Name = "Sale", PositionId = 1, Overall = 82 };

        context.Teams.AddRange(originTeam, destinationTeam);
        context.Players.Add(player);
        context.TeamRosters.Add(new TeamRoster { TeamId = originTeam.TeamId, PlayerId = player.PlayerId });
        context.TeamBudgets.Add(new TeamBudget { TeamId = destinationTeam.TeamId, Saldo = 150m });
        await context.SaveChangesAsync();

        var service = new NegotiationService(context, timeProvider);

        var createDto = new NegotiationCreateDto(
            originTeam.TeamToken.ToString(),
            destinationTeam.TeamToken.ToString(),
            "SALE",
            100m,
            new[] { player.PlayerId },
            Array.Empty<int>(),
            null);

        var negotiation = await service.CreateAsync(createDto, CancellationToken.None);

        var destinationBudget = await context.TeamBudgets.FindAsync(destinationTeam.TeamId);
        destinationBudget!.Saldo = 50m;
        await context.SaveChangesAsync();

        var responseDto = new NegotiationResponseDto(destinationTeam.TeamToken.ToString(), "ACCEPT");

        await Assert.ThrowsAsync<NegotiationConflictException>(() => service.RespondAsync(negotiation.NegotiationId, responseDto, CancellationToken.None));

        var pending = await context.Negotiations.FindAsync(negotiation.NegotiationId);
        Assert.Equal("PENDING", pending!.Status);
    }

    [Fact]
    public async Task CancelAndRejectNegotiations_UpdateStatus()
    {
        using var context = CreateDbContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.UtcNow);

        var originTeam = new Team { TeamId = Guid.NewGuid(), TeamName = "Origin", TeamToken = Guid.NewGuid() };
        var destinationTeam = new Team { TeamId = Guid.NewGuid(), TeamName = "Destination", TeamToken = Guid.NewGuid() };
        var player = new Player { PlayerId = 50, Name = "Cancel", PositionId = 1, Overall = 79 };

        context.Teams.AddRange(originTeam, destinationTeam);
        context.Players.Add(player);
        context.TeamRosters.Add(new TeamRoster { TeamId = originTeam.TeamId, PlayerId = player.PlayerId });
        context.TeamBudgets.Add(new TeamBudget { TeamId = destinationTeam.TeamId, Saldo = 200m });
        await context.SaveChangesAsync();

        var service = new NegotiationService(context, timeProvider);

        var createDto = new NegotiationCreateDto(
            originTeam.TeamToken.ToString(),
            destinationTeam.TeamToken.ToString(),
            "SALE",
            80m,
            new[] { player.PlayerId },
            Array.Empty<int>(),
            null);

        var negotiation = await service.CreateAsync(createDto, CancellationToken.None);

        await service.CancelAsync(negotiation.NegotiationId, originTeam.TeamId, CancellationToken.None);

        var cancelled = await context.Negotiations.FindAsync(negotiation.NegotiationId);
        Assert.Equal("CANCELLED", cancelled!.Status);
        Assert.NotNull(cancelled.DataFechamentoUtc);

        var newNegotiation = await service.CreateAsync(createDto, CancellationToken.None);
        var responseDto = new NegotiationResponseDto(destinationTeam.TeamToken.ToString(), "REJECT");
        var rejected = await service.RespondAsync(newNegotiation.NegotiationId, responseDto, CancellationToken.None);

        Assert.Equal("REJECTED", rejected.Status);
        Assert.NotNull(rejected.DataFechamentoUtc);
    }

    private static DraftDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DraftDbContext(options);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            FixedUtcNow = utcNow;
        }

        public DateTimeOffset FixedUtcNow { get; }

        public override DateTimeOffset GetUtcNow() => FixedUtcNow;
    }
}
