using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaGoleiroPartidaConfiguration : IEntityTypeConfiguration<LigaGoleiroPartida>
{
    public void Configure(EntityTypeBuilder<LigaGoleiroPartida> b)
    {
        b.ToTable("LigaGoleirosPartida");
        b.HasKey(x => new { x.PartidaId, x.TimeId });

        b.HasOne(x => x.Partida).WithMany().HasForeignKey(x => x.PartidaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Jogador).WithMany().HasForeignKey(x => x.JogadorId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.JogadorId);
    }
}
