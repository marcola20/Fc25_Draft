using System.Linq;
using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Data;

public static class SeedData
{
    private static readonly (string OwnerName, string TeamName, string Token)[] DefaultTeams =
    [
        ("Renan", "Manaus", "28D81F84-A1FA-4F2D-BAD7-58D50057C2E2"),
        ("JG", "Madureira", "9EF8FCA9-4DC1-40F3-915D-97B20F2AFB80"),
        ("Pio", "São Bernardo", "6DBDED67-E531-4AEC-8343-EF6733B66119"),
        ("Mafra", "Volta Redonda", "76EC6641-1990-4366-AAD1-CC6E5FB327F3"),
        ("Guilherme", "Corinthians", "04603E8C-FA5E-42D5-B6D6-3F9042DCDB36"),
        ("Kaio", "Santa Cruz", "C2DE6E43-3A38-4867-8D17-E256EAD82F10"),
        ("João", "Asa de Arapiraca", "090879CC-239B-430B-8F03-868C7A3ED4B2"),
        ("Mateuzinho", "Amazonas", "A94615F5-84A6-43C8-A71A-24336A9AEA44"),
        ("Gui Gomes", "Paysandu", "A7F9E3AF-26BE-448D-97BD-04958D623E19"),
        ("Marcola", "Remo", "20AN4YSS4-ANA1-C4M1-1909COXA1909"),
        ("Rafa", "Nautico", "B83E4C5F-464B-46B7-ACF4-0311884207A9"),
        ("Portuga", "Anápolis", "9EA2492B-9D04-47F7-BF5A-5055E9133F25"),
        ("L. Felipe", "Portuguesa", "636C4055-F5DA-486C-BA10-4288FACE1150"),
        ("Raphael", "Mirassol", "89E22A22-E3A0-4960-980D-973AED414F7F")
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
