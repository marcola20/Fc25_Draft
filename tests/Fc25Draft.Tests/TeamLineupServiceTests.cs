using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Fc25Draft.Tests;

public class TeamLineupServiceTests
{
    private static readonly FormationSlotFactory SlotFactory = new();

    [Fact]
    public async Task SaveAsync_CreatesActiveLineupAndDeactivatesPrevious()
    {
        await using var context = await CreateContextAsync();
        var teamId = await SeedTeamAsync(context);
        var service = new TeamLineupService(context, SlotFactory, NullLogger<TeamLineupService>.Instance);

        var firstRequest = BuildValidRequest();
        var firstResponse = await service.SaveAsync(teamId, firstRequest, CancellationToken.None);

        Assert.Equal("4-2-4", firstResponse.FormationCode);
        Assert.Equal("TAT-001", firstResponse.TacticCode);
        Assert.Equal(18, firstResponse.Slots.Count);
        Assert.Equal(18, await context.TeamLineupSlots.CountAsync());

        var secondRequest = BuildValidRequest(tacticCode: "TAT-002");
        var secondResponse = await service.SaveAsync(teamId, secondRequest, CancellationToken.None);

        Assert.Equal("TAT-002", secondResponse.TacticCode);
        Assert.Equal(2, await context.TeamLineups.CountAsync());
        Assert.Single(await context.TeamLineups.Where(l => l.IsActive).ToListAsync());
    }

    [Fact]
    public async Task SaveAsync_WhenPlayerDuplicated_ThrowsInvalidOperationException()
    {
        await using var context = await CreateContextAsync();
        var teamId = await SeedTeamAsync(context);
        var service = new TeamLineupService(context, SlotFactory, NullLogger<TeamLineupService>.Instance);

        var request = BuildValidRequest();
        request.Slots[1] = request.Slots[1] with { PlayerId = request.Slots[0].PlayerId };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(teamId, request, CancellationToken.None));
        Assert.Contains("Não é permitido repetir", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenPlayerNotEligible_ThrowsInvalidOperationException()
    {
        await using var context = await CreateContextAsync();
        var teamId = await SeedTeamAsync(context);
        var service = new TeamLineupService(context, SlotFactory, NullLogger<TeamLineupService>.Instance);

        var request = BuildValidRequest();
        request.Slots[0] = request.Slots[0] with { PlayerId = request.Slots[2].PlayerId };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(teamId, request, CancellationToken.None));
        Assert.Contains("não é elegível", ex.Message);
    }

    [Fact]
    public async Task SaveAsync_WhenMissingPlayer_ThrowsInvalidOperationException()
    {
        await using var context = await CreateContextAsync();
        var teamId = await SeedTeamAsync(context);
        var service = new TeamLineupService(context, SlotFactory, NullLogger<TeamLineupService>.Instance);

        var request = BuildValidRequest();
        request.Slots[0] = request.Slots[0] with { PlayerId = null };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(teamId, request, CancellationToken.None));
        Assert.Contains("Todos os slots", ex.Message);
    }

    private static SaveLineupRequest BuildValidRequest(string tacticCode = "TAT-001")
    {
        var templates = SlotFactory.Build("4-2-4");
        var playerIds = Enumerable.Range(1, 18).ToArray();

        var slots = templates
            .OrderBy(t => t.Order)
            .Select((template, index) => new SaveLineupSlotDto
            {
                Order = template.Order,
                Role = template.Role,
                PrimaryPositionId = template.PrimaryPositionId,
                PlayerId = playerIds[index]
            })
            .ToList();

        return new SaveLineupRequest
        {
            FormationCode = "4-2-4",
            TacticCode = tacticCode,
            Slots = slots
        };
    }

    private static async Task<DraftDbContext> CreateContextAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new DraftDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static async Task<Guid> SeedTeamAsync(DraftDbContext context)
    {
        var teamId = Guid.NewGuid();
        context.Teams.Add(new Team
        {
            TeamId = teamId,
            TeamName = "Time Teste",
            OwnerName = "Usuário",
            Token = "TOKEN",
            Budget = 1_000_000m,
            BudgetBlocked = 0m
        });

        var positionIds = new[] { 1, 3, 2, 2, 4, 5, 6, 8, 9, 10, 10, 1, 2, 4, 5, 6, 8, 10 };
        for (var i = 0; i < positionIds.Length; i++)
        {
            var playerId = i + 1;
            context.Players.Add(new Player
            {
                PlayerId = playerId,
                PlayerGuid = Guid.NewGuid(),
                Name = $"Jogador {playerId}",
                Overall = 80,
                PositionId = (short)positionIds[i],
                CurrentTeamId = teamId
            });

            context.TeamRosters.Add(new TeamRoster
            {
                TeamId = teamId,
                PlayerId = playerId
            });
        }

        await context.SaveChangesAsync();
        return teamId;
    }
}
