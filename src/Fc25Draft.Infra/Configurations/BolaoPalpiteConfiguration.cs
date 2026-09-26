using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class BolaoPalpiteConfiguration : IEntityTypeConfiguration<BolaoPalpite>
{
    public void Configure(EntityTypeBuilder<BolaoPalpite> b)
    {
        b.ToTable("BolaoPalpites");
        b.HasKey(x => x.PalpiteId);

        // Um palpite por pessoa em cada jogo.
        b.HasIndex(x => new { x.TreinadorId, x.PartidaId }).IsUnique();
        b.HasIndex(x => x.PartidaId);

        b.HasOne(x => x.Treinador)
            .WithMany()
            .HasForeignKey(x => x.TreinadorId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Partida)
            .WithMany()
            .HasForeignKey(x => x.PartidaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
