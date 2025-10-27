using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fc25Draft.Tests;

public class BudgetServiceTests
{
    [Fact]
    public void CalculateMatchRewardAmount_ReturnsExpectedValueForVictory()
    {
        using var context = CreateDbContext();
        var team = SeedTeam(context);
        var service = CreateService(context);

        var request = new MatchRewardRequestDto(team.TeamId, 2, 0, true, "VITORIA");

        var amount = service.CalculateMatchRewardAmount(request);

        Assert.Equal(3_900_000m, amount);
    }

    [Fact]
    public void CalculateMatchRewardAmount_ReturnsNegativeWhenPenaltiesExceedRewards()
    {
        using var context = CreateDbContext();
        var team = SeedTeam(context);
        var service = CreateService(context);

        var request = new MatchRewardRequestDto(team.TeamId, 0, 4, false, "DERROTA");

        var amount = service.CalculateMatchRewardAmount(request);

        Assert.Equal(-400_000m, amount);
    }

    [Fact]
    public async Task ApplyMatchRewardAsync_CreatesLedgerEntryAndUpdatesSaldo()
    {
        using var context = CreateDbContext();
        var team = SeedTeam(context);
        context.TeamBudgets.Add(new TeamBudget { TeamId = team.TeamId, Saldo = 10_000_000m });
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var request = new MatchRewardRequestDto(team.TeamId, 1, 0, false, "EMPATE");

        var result = await service.ApplyMatchRewardAsync(request, CancellationToken.None);

        Assert.True(result.AjusteRealizado);
        Assert.Equal(1_200_000m, result.ValorAplicado);
        Assert.Equal("CREDIT", result.Tipo);

        var budget = await context.TeamBudgets.SingleAsync(b => b.TeamId == team.TeamId);
        Assert.Equal(11_200_000m, budget.Saldo);

        var ledgerEntry = await context.BudgetLedgers.SingleAsync(l => l.TeamId == team.TeamId);
        Assert.Equal(1_200_000m, ledgerEntry.Valor);
        Assert.Equal("MATCH_REWARD", ledgerEntry.Origem);
        Assert.Equal("CREDIT", ledgerEntry.Tipo);
    }

    private static DraftDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new DraftDbContext(options);
    }

    private static Team SeedTeam(DraftDbContext context)
    {
        var team = new Team
        {
            TeamId = Guid.NewGuid(),
            TeamName = "Time Teste",
            TeamToken = Guid.NewGuid()
        };

        context.Teams.Add(team);
        context.SaveChanges();
        return team;
    }

    private static BudgetService CreateService(DraftDbContext context)
    {
        var options = Options.Create(new EconomiaOptions
        {
            PremioVitoria = 3_000_000m,
            PremioEmpate = 1_000_000m,
            PremioGolMarcado = 200_000m,
            PremioCleanSheet = 500_000m,
            PenalidadeGolSofrido = 100_000m
        });

        return new BudgetService(context, options, new FixedTimeProvider(DateTimeOffset.UtcNow));
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
