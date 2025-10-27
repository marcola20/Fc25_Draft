using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Data;
using Fc25Draft.Infra.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fc25Draft.Tests;

public class PricingServiceTests
{
    [Fact]
    public async Task CalculateAsync_GoalkeeperSample()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        var result = await service.CalculateAsync("GK", null, 24, 81);

        Assert.Equal(12.3m, result.PrecoBase);
        Assert.Equal(9.8m, result.LanceInicial);
        Assert.Equal(18.5m, result.ComprarAgora);
    }

    [Fact]
    public async Task CalculateAsync_StrikerSample()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        var result = await service.CalculateAsync("ST", null, 21, 77);

        Assert.Equal(12.4m, result.PrecoBase);
        Assert.Equal(9.9m, result.LanceInicial);
        Assert.Equal(18.6m, result.ComprarAgora);
    }

    [Fact]
    public async Task CalculateAsync_CentralMidfielderSample()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        var result = await service.CalculateAsync("CM", null, 30, 79);

        Assert.Equal(11.2m, result.PrecoBase);
        Assert.Equal(9.0m, result.LanceInicial);
        Assert.Equal(16.8m, result.ComprarAgora);
    }

    [Fact]
    public void RoundToTenth_RoundsAsExpected()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        Assert.Equal(12.3m, service.RoundToTenth(12.34m));
        Assert.Equal(12.4m, service.RoundToTenth(12.36m));
        Assert.Equal(18.5m, service.RoundToTenth(18.45m));
    }

    [Fact]
    public void NextMinIncrement_ComputesExpectedValues()
    {
        using var context = CreateDbContext();
        var service = CreateService(context);

        Assert.Equal(0.5m, service.NextMinIncrement(10.0m));
        Assert.Equal(0.2m, service.NextMinIncrement(1.1m));
    }

    [Fact]
    public async Task CalculateForPlayerAsync_UsesPlayerData()
    {
        using var context = CreateDbContext();
        context.Positions.Add(new Position { PositionId = 10, Name = "Atacante" });
        context.Players.Add(new Player
        {
            PlayerId = 123,
            Name = "Teste",
            Age = 24,
            Overall = 81,
            PositionId = 10
        });
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.CalculateForPlayerAsync(123);

        Assert.Equal(12.3m, result.PrecoBase);
        Assert.Equal(9.8m, result.LanceInicial);
        Assert.Equal(18.5m, result.ComprarAgora);
    }

    private static DraftDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DraftDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new DraftDbContext(options);
    }

    private static PricingService CreateService(DraftDbContext context)
    {
        var options = Options.Create(new PricingOptions());
        return new PricingService(context, options);
    }
}
