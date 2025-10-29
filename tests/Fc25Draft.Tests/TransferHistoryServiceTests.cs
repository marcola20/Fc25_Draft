using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fc25Draft.Tests;

public class TransferHistoryServiceTests
{
    [Fact]
    public async Task RegisterTransferAsync_Throws_WhenPlayerDoesNotExist()
    {
        await using var context = CreateContext();
        var service = new TransferHistoryService(context, TimeProvider.System);

        var entry = new TransferHistory
        {
            TransferId = Guid.NewGuid(),
            PlayerId = 999,
            Type = TransferType.TeamSale,
            PerformedAtUtc = DateTime.UtcNow
        };

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterTransferAsync(entry));
    }

    [Fact]
    public async Task RegisterTransferAsync_PersistsEntry_WithNormalizedValues()
    {
        await using var context = CreateContext();
        SeedPlayerAndTeams(context);

        var service = new TransferHistoryService(context, TimeProvider.System);

        var performedAt = new DateTime(2024, 1, 10, 8, 30, 0, DateTimeKind.Unspecified);
        var entry = new TransferHistory
        {
            TransferId = Guid.Empty,
            PlayerId = 1,
            FromTeamId = TestData.FromTeamId,
            ToTeamId = TestData.ToTeamId,
            Amount = 150m,
            Type = TransferType.TeamSale,
            Notes = new string('n', 450),
            PerformedBy = new string('p', 140),
            PerformedAtUtc = performedAt
        };

        await service.RegisterTransferAsync(entry);

        var saved = await context.TransferHistories
            .AsNoTracking()
            .FirstAsync();

        Assert.NotEqual(Guid.Empty, saved.TransferId);
        Assert.Equal(TestData.FromTeamId, saved.FromTeamId);
        Assert.Equal(TestData.ToTeamId, saved.ToTeamId);
        Assert.Equal(150m, saved.Amount);
        Assert.Equal(TransferType.TeamSale, saved.Type);
        Assert.Equal(400, saved.Notes!.Length);
        Assert.Equal(120, saved.PerformedBy!.Length);
        Assert.Equal(DateTimeKind.Utc, saved.PerformedAtUtc.Kind);
    }

    [Fact]
    public async Task GetTransfersByTeamAsync_ReturnsOrderedHistory()
    {
        await using var context = CreateContext();
        SeedPlayerAndTeams(context);

        context.TransferHistories.AddRange(
            new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                PlayerId = 1,
                FromTeamId = TestData.FromTeamId,
                ToTeamId = TestData.ToTeamId,
                Type = TransferType.TeamSale,
                PerformedAtUtc = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc)
            },
            new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                PlayerId = 1,
                FromTeamId = TestData.FromTeamId,
                ToTeamId = TestData.ToTeamId,
                Type = TransferType.TeamTrade,
                PerformedAtUtc = new DateTime(2024, 2, 1, 12, 0, 0, DateTimeKind.Utc)
            },
            new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                PlayerId = 1,
                FromTeamId = TestData.ToTeamId,
                ToTeamId = TestData.FromTeamId,
                Type = TransferType.TeamTrade,
                PerformedAtUtc = new DateTime(2024, 3, 1, 12, 0, 0, DateTimeKind.Utc)
            });

        await context.SaveChangesAsync();

        var service = new TransferHistoryService(context, TimeProvider.System);

        var result = await service.GetTransfersByTeamAsync(TestData.FromTeamId, take: 2);

        Assert.Equal(2, result.Count);
        Assert.True(result[0].PerformedAtUtc >= result[1].PerformedAtUtc);
        Assert.All(result, r => Assert.True(r.FromTeamId == TestData.FromTeamId || r.ToTeamId == TestData.FromTeamId));
    }

    [Fact]
    public async Task GetTransfersByTeamAsync_ThrowsForUnknownTeam()
    {
        await using var context = CreateContext();
        SeedPlayerAndTeams(context);

        var service = new TransferHistoryService(context, TimeProvider.System);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetTransfersByTeamAsync(Guid.NewGuid()));
    }

    private static DraftDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DraftDbContext(options);
    }

    private static void SeedPlayerAndTeams(DraftDbContext context)
    {
        if (!context.Positions.Any())
        {
            context.Positions.Add(new Position
            {
                PositionId = 1,
                Name = "Goleiro"
            });
        }

        if (!context.Players.Any())
        {
            context.Players.Add(new Player
            {
                PlayerId = 1,
                Name = "Jogador Teste",
                Overall = 80,
                Age = 25,
                PositionId = 1,
                PlayerGuid = Guid.NewGuid()
            });
        }

        if (!context.Teams.Any())
        {
            context.Teams.AddRange(
                new Team
                {
                    TeamId = TestData.FromTeamId,
                    TeamName = "Origem",
                    OwnerName = "Origem",
                    Token = Guid.NewGuid().ToString(),
                    Budget = 1000m,
                    BudgetBlocked = 0m
                },
                new Team
                {
                    TeamId = TestData.ToTeamId,
                    TeamName = "Destino",
                    OwnerName = "Destino",
                    Token = Guid.NewGuid().ToString(),
                    Budget = 1000m,
                    BudgetBlocked = 0m
                });
        }

        context.SaveChanges();
    }

    private static class TestData
    {
        public static readonly Guid FromTeamId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        public static readonly Guid ToTeamId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    }
}
