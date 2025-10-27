using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Data;

public static class SeedData
{
    private static readonly (string OwnerName, string TeamName)[] DefaultTeams =
    [
        ("Renan", "Manaus"),
        ("João", "Madureira"),
        ("Pio", "São Bernardo"),
        ("Mafra", "Volta Redonda"),
        ("Guilherme", "Corinthians"),
        ("Kaio", "Santa Cruz"),
        ("João", "Asa de Arapiraca"),
        ("Mateuzinho", "Amazonas"),
        ("Gui Gomes", "Paysandu"),
        ("Marcola", "Remo"),
        ("Rafa", "Sousa"),
        ("Portuga", "Anápolis"),
        ("L. Felipe", "Portuguesa"),
        ("Raphael", "Mirassol"),
    ];

    public static async Task SeedAsync(DraftDbContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var hasChanges = false;

        if (!await context.Teams.AnyAsync(cancellationToken))
        {
            foreach (var (ownerName, teamName) in DefaultTeams)
            {
                context.Teams.Add(new Team
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = teamName,
                    OwnerName = ownerName,
                    TeamToken = Guid.NewGuid()
                });
            }

            hasChanges = true;
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
