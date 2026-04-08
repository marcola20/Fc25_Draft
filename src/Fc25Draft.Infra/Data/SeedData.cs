using System.Linq;
using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Data;

public static class SeedData
{
    private static readonly (string OwnerName, string TeamName, string Token)[] DefaultTeams =
    [
        ("João",       "João",       "1a2b3c4d-0001-0001-0001-000000000001"),
        ("Mafra",      "Mafra",      "76EC6641-1990-4366-AAD1-CC6E5FB327F3"),
        ("Rafa",       "Rafa",       "B83E4C5F-464B-46B7-ACF4-0311884207A9"),
        ("L. Felipe",  "L. Felipe",  "636C4055-F5DA-486C-BA10-4288FACE1150"),
        ("Albert",     "Albert",     "A94615F5-84A6-43C8-A71A-24336A9AEA44"),
        ("Pio",        "Pio",        "6DBDED67-E531-4AEC-8343-EF6733B66119"),
        ("Kaio",       "Kaio",       "54NT05C4-K410-1B24-2023-S4NT0SK4101B"),
        ("Jotage",     "Jotage",     "9EF8FCA9-4DC1-40F3-915D-97B20F2AFB80"),
        ("Renan",      "Renan",      "28D81F84-A1FA-4F2D-BAD7-58D50057C2E2"),
        ("Guilherme",  "Guilherme",  "04603E8C-FA5E-42D5-B6D6-3F9042DCDB36"),
        ("Gui Gomes",  "Gui Gomes",  "A7F9E3AF-26BE-448D-97BD-04958D623E19"),
        ("Marcola",    "Marcola",    "20AN4YSS4-ANA1-C4M1-1909COXA1909"),
    ];

    public static async Task SeedAsync(DraftDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var hasChanges = false;

        if (!await context.Teams.AnyAsync(cancellationToken))
        {
            foreach (var (ownerName, teamName, token) in DefaultTeams)
            {
                context.Teams.Add(new Team
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = teamName,
                    OwnerName = ownerName,
                    Token = token,
                    Budget = 50_000_000m,
                    BudgetBlocked = 0m
                });
            }

            hasChanges = true;
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    public static async Task SeedTeamBudgetsAsync(DraftDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var teams = await context.Teams.ToListAsync(cancellationToken);

        foreach (var team in teams)
        {
            if (team.Budget <= 0m)
            {
                team.Budget = 50_000_000m;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
