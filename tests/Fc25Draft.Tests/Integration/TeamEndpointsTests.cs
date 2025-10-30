using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Xunit;

namespace Fc25Draft.Tests.Integration;

public class TeamEndpointsTests
{
    [Fact]
    public async Task Roster_ReturnsEmptyArray_WhenNoTeamsExist()
    {
        var factory = new TeamEndpointsFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/teams/roster");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<List<TeamRosterDto>>();
        Assert.NotNull(payload);
        Assert.Empty(payload!);
    }

    [Fact]
    public async Task Roster_HandlesPlayersWithoutPosition()
    {
        var factory = new TeamEndpointsFactory();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();
            await db.Database.EnsureCreatedAsync();

            var teamId = Guid.NewGuid();
            var playerId = 1;

            var team = new Team
            {
                TeamId = teamId,
                TeamName = "Team Alpha",
                OwnerName = "Owner",
                Token = Guid.NewGuid().ToString("N"),
                Budget = 0m,
                BudgetBlocked = 0m
            };

            var player = new Player
            {
                PlayerId = playerId,
                PlayerGuid = Guid.NewGuid(),
                Name = "Player One",
                Overall = 90,
                PositionId = 99,
                CurrentTeamId = teamId,
                Position = null!
            };

            var roster = new TeamRoster
            {
                TeamId = teamId,
                PlayerId = playerId,
                Team = team,
                Player = player
            };

            team.Roster.Add(roster);
            player.TeamRosters.Add(roster);

            db.Teams.Add(team);
            db.Players.Add(player);
            db.TeamRosters.Add(roster);

            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/teams/roster");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<List<TeamRosterDto>>();
        Assert.NotNull(payload);
        Assert.Single(payload!);
        var players = payload![0].Jogadores;
        Assert.Single(players);
        Assert.Equal(string.Empty, players[0].Posicao);
    }
}

public sealed class TeamEndpointsFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<DraftDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<DraftDbContext>(options =>
                options.UseInMemoryDatabase($"teams-{Guid.NewGuid():N}"));
        });
    }
}
