using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Data;

public static class DevSeeder
{
    public static async Task SeedAsync(DraftDbContext db, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);

        await SeedData.SeedAsync(db, cancellationToken);

        if (!await db.Players.AnyAsync(cancellationToken))
        {
            db.Players.AddRange(
                new Player
                {
                    PlayerId = 1,
                    PlayerGuid = Guid.NewGuid(),
                    Name = "Jogador 1",
                    Overall = 85,
                    Age = 28,
                    PositionId = 1
                },
                new Player
                {
                    PlayerId = 2,
                    PlayerGuid = Guid.NewGuid(),
                    Name = "Jogador 2",
                    Overall = 83,
                    Age = 26,
                    PositionId = 10
                }
            );
        }

        if (!await db.Teams.AnyAsync(cancellationToken))
        {
            db.Teams.AddRange(
                new Team
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "Time A",
                    OwnerName = "Owner A",
                    Token = Guid.NewGuid().ToString(),
                    Budget = 50_000_000m,
                    BudgetBlocked = 0m
                },
                new Team
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = "Time B",
                    OwnerName = "Owner B",
                    Token = Guid.NewGuid().ToString(),
                    Budget = 50_000_000m,
                    BudgetBlocked = 0m
                }
            );
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
