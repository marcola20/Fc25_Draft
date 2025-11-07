using System.Net;
using System.Net.Http.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Fc25Draft.Tests.Integration;

public class TeamQuickSellEndpointsTests : IClassFixture<TeamEndpointsFactory>
{
    private readonly TeamEndpointsFactory _factory;

    public TeamQuickSellEndpointsTests(TeamEndpointsFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task QuickSell_ReturnsResult_WhenValid()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var client = factory.CreateClient();
        var token = await SeedScenarioAsync(factory.Services, rosterSize: 19);

        using var response = await client.PostAsJsonAsync($"/api/teams/{token.TeamId}/quick-sell/{token.PlayerId}", new { TeamToken = token.Token });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<QuickSellResultDto>();
        Assert.NotNull(dto);
        Assert.Equal(token.TeamId, dto!.TeamId);
        Assert.Equal(token.PlayerId, dto.PlayerId);
        Assert.Equal(PlayerStatus.FreeAgent, dto.Status);
        Assert.Equal(decimal.Round(dto.BasePrice * 0.8m, 2, MidpointRounding.AwayFromZero), dto.Payout);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();

        var rosterEntry = await db.TeamRosters.AsNoTracking().FirstOrDefaultAsync(r => r.TeamId == token.TeamId && r.PlayerId == token.PlayerId);
        Assert.Null(rosterEntry);

        var history = await db.TransferHistories.AsNoTracking().SingleAsync();
        Assert.Equal(TransferType.QuickSell, history.Type);
        Assert.Equal(dto.Payout, history.Payout);
        Assert.Equal(dto.NewOverall, history.NewOverall);
    }

    [Fact]
    public async Task QuickSell_ReturnsConflict_WhenRosterBelowMinimum()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var client = factory.CreateClient();
        var token = await SeedScenarioAsync(factory.Services, rosterSize: 18);

        using var response = await client.PostAsJsonAsync($"/api/teams/{token.TeamId}/quick-sell/{token.PlayerId}", new { TeamToken = token.Token });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task QuickSell_ReturnsForbidden_WhenTokenInvalid()
    {
        var factory = _factory.WithWebHostBuilder(_ => { });
        var client = factory.CreateClient();
        var token = await SeedScenarioAsync(factory.Services, rosterSize: 19);

        using var response = await client.PostAsJsonAsync($"/api/teams/{token.TeamId}/quick-sell/{token.PlayerId}", new { TeamToken = "wrong" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<(Guid TeamId, int PlayerId, string Token)> SeedScenarioAsync(IServiceProvider services, int rosterSize)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DraftDbContext>();

        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        if (!db.Positions.Any())
        {
            db.Positions.Add(new Position { PositionId = 1, Name = "Goleiro" });
            await db.SaveChangesAsync();
        }

        var teamId = Guid.NewGuid();
        var token = Guid.NewGuid().ToString("N").ToUpperInvariant();
        var team = new Team
        {
            TeamId = teamId,
            TeamName = "Time",
            OwnerName = "Owner",
            Token = token,
            Budget = 150_000_000m,
            BudgetBlocked = 0m
        };

        db.Teams.Add(team);

        for (var i = 0; i < rosterSize; i++)
        {
            var playerId = i + 1;
            var player = new Player
            {
                PlayerId = playerId,
                PlayerGuid = Guid.NewGuid(),
                Name = $"Jogador {playerId}",
                Age = 27,
                Overall = 75 + i % 3,
                Status = PlayerStatus.Active,
                PositionId = 1,
                CurrentTeamId = teamId
            };

            db.Players.Add(player);
            db.TeamRosters.Add(new TeamRoster { TeamId = teamId, PlayerId = playerId, Team = team, Player = player });
        }

        await db.SaveChangesAsync();

        return (teamId, 1, token);
    }
}

public sealed class TeamEndpointsFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<DraftDbContext>>();
            services.AddDbContext<DraftDbContext>(options =>
                options.UseInMemoryDatabase($"teams-quick-sell-{Guid.NewGuid():N}"));
        });
    }
}
