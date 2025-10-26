using Fc25Draft.Core.Entities;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Data;

public static class SeedData
{
    public static async Task SeedAsync(DraftDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var hasChanges = false;

        if (!await context.Positions.AnyAsync(cancellationToken))
        {
            var positions = new List<Position>
            {
                new() { PositionId = 1, Name = "GK" },
                new() { PositionId = 2, Name = "DEF" },
                new() { PositionId = 3, Name = "MID" },
                new() { PositionId = 4, Name = "FWD" }
            };

            context.Positions.AddRange(positions);
            hasChanges = true;
        }

        if (!await context.Teams.AnyAsync(cancellationToken))
        {
            context.Teams.Add(new Team
            {
                TeamId = Guid.NewGuid(),
                TeamName = "Falcons City",
                OwnerName = "Manager 1",
                TeamToken = Guid.NewGuid()
            });

            hasChanges = true;
        }

        if (!await context.Players.AnyAsync(cancellationToken))
        {
            var players = new List<Player>
            {
                new() { Name = "Alex Keeper", Age = 28, Overall = 81, PositionId = 1 },
                new() { Name = "Marco Back", Age = 27, Overall = 79, PositionId = 2 },
                new() { Name = "Davi Center", Age = 24, Overall = 83, PositionId = 3 },
                new() { Name = "Rui Striker", Age = 26, Overall = 85, PositionId = 4 }
            };

            context.Players.AddRange(players);
            hasChanges = true;
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
