using Fc25Draft.Web.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Data
{
    public static class SeedData
    {
        public static async Task SeedAsync(AppDbContext context, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);

            var hasChanges = false;

            if (!await context.Teams.AnyAsync(cancellationToken))
            {
                var teams = new List<Team>
                {
                    new() { TeamId = Guid.NewGuid(), TeamName = "Atléticos da Vila", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Corujas FC", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Lobos do Norte", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Estrelas da Serra", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Guerrilha Paulista", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Maré Azul", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Dragoes Cariocas", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Raposas Mineiras", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Falcões do Oeste", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Tridentes do Sul", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Força Amazônica", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Cangaceiros FC", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Guaranis Atléticos", TeamToken = Guid.NewGuid() },
                    new() { TeamId = Guid.NewGuid(), TeamName = "Vitória Imperial", TeamToken = Guid.NewGuid() }
                };

                context.Teams.AddRange(teams);
                hasChanges = true;
            }

            if (!await context.Players.AnyAsync(cancellationToken))
            {
                var players = new List<Player>
                {
                    new() { Name = "Carlos Mota", Age = 29, Overall = 88, PositionId = 1 },
                    new() { Name = "Rafael Dutra", Age = 27, Overall = 85, PositionId = 1 },
                    new() { Name = "Henrique Silva", Age = 25, Overall = 82, PositionId = 2 },
                    new() { Name = "João Vitor", Age = 28, Overall = 84, PositionId = 2 },
                    new() { Name = "Marcos Cunha", Age = 24, Overall = 80, PositionId = 2 },
                    new() { Name = "Tiago Ramos", Age = 27, Overall = 83, PositionId = 3 },
                    new() { Name = "André Freitas", Age = 26, Overall = 81, PositionId = 3 },
                    new() { Name = "Fabrício Lemos", Age = 29, Overall = 84, PositionId = 4 },
                    new() { Name = "Sérgio Prado", Age = 25, Overall = 82, PositionId = 4 },
                    new() { Name = "Eduardo Nunes", Age = 27, Overall = 86, PositionId = 5 },
                    new() { Name = "Bruno Paiva", Age = 24, Overall = 83, PositionId = 5 },
                    new() { Name = "Felipe Tavares", Age = 26, Overall = 85, PositionId = 6 },
                    new() { Name = "Vinícius Lopes", Age = 25, Overall = 84, PositionId = 6 },
                    new() { Name = "Lucas Ferraz", Age = 23, Overall = 82, PositionId = 6 },
                    new() { Name = "Matheus Araújo", Age = 27, Overall = 87, PositionId = 7 },
                    new() { Name = "Ricardo Campos", Age = 28, Overall = 86, PositionId = 7 },
                    new() { Name = "Douglas Vieira", Age = 24, Overall = 83, PositionId = 7 },
                    new() { Name = "Pedro Martins", Age = 22, Overall = 81, PositionId = 8 },
                    new() { Name = "Igor Santana", Age = 25, Overall = 84, PositionId = 8 },
                    new() { Name = "Marcelo Pires", Age = 27, Overall = 85, PositionId = 8 },
                    new() { Name = "Wesley Rocha", Age = 26, Overall = 84, PositionId = 9 },
                    new() { Name = "Leandro Costa", Age = 24, Overall = 82, PositionId = 9 },
                    new() { Name = "Gustavo Novaes", Age = 28, Overall = 83, PositionId = 9 },
                    new() { Name = "Alex Teixeira", Age = 27, Overall = 90, PositionId = 10 },
                    new() { Name = "Bruno Almeida", Age = 25, Overall = 88, PositionId = 10 },
                    new() { Name = "Daniel Ribeiro", Age = 23, Overall = 85, PositionId = 10 },
                    new() { Name = "Samuel Farias", Age = 24, Overall = 83, PositionId = 5 },
                    new() { Name = "Edson Moraes", Age = 29, Overall = 86, PositionId = 4 },
                    new() { Name = "Caique Moreira", Age = 22, Overall = 80, PositionId = 3 },
                    new() { Name = "Luiz Henrique", Age = 26, Overall = 77, PositionId = 2 }
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
}
