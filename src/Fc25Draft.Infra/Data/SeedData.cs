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

        if (!await context.Teams.AnyAsync(cancellationToken))
        {
            var teams = new (string OwnerName, string TeamName)[]
            {
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
                ("L. Felipe", "Coritiba"),
                ("Raphael", "Mirassol"),
            };

            foreach (var t in teams)
            {
                context.Teams.Add(new Team
                {
                    TeamId = Guid.NewGuid(),
                    TeamName = t.TeamName,
                    OwnerName = t.OwnerName,
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
