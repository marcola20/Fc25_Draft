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
        ("L. Felipe", "Coritiba"),
        ("Raphael", "Mirassol"),
    ];

    private static readonly (string Name, int? Age, int Overall, short PositionId)[] DefaultPlayers =
    [
        ("Carlos Almeida", 28, 82, 1),
        ("Diego Ramos", 24, 80, 1),
        ("Henrique Santos", 26, 78, 2),
        ("Rafael Moreira", 29, 81, 2),
        ("Matheus Pires", 25, 79, 3),
        ("Pedro Arantes", 23, 77, 3),
        ("Caio Teixeira", 27, 80, 4),
        ("Igor Barreto", 30, 76, 4),
        ("Lucas Vidal", 28, 83, 5),
        ("Gabriel Nogueira", 24, 78, 5),
        ("Ruan Batista", 26, 84, 6),
        ("Felipe Moretti", 22, 79, 6),
        ("Allan Peixoto", 27, 82, 7),
        ("Vitor Miranda", 25, 80, 7),
        ("Luan Azevedo", 23, 81, 8),
        ("Thiago Silveira", 28, 79, 8),
        ("Bruno Farias", 26, 82, 9),
        ("João Pedro", 21, 77, 9),
        ("Marcelo Tavares", 29, 85, 10),
        ("Eduardo Lopes", 24, 83, 10),
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

        if (!await context.Players.AnyAsync(cancellationToken))
        {
            foreach (var (name, age, overall, positionId) in DefaultPlayers)
            {
                context.Players.Add(new Player
                {
                    Name = name,
                    Age = age,
                    Overall = overall,
                    PositionId = positionId
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
