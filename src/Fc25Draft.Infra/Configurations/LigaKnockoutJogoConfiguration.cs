using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaKnockoutJogoConfiguration : IEntityTypeConfiguration<LigaKnockoutJogo>
{
    public void Configure(EntityTypeBuilder<LigaKnockoutJogo> e)
    {
        e.HasKey(x => x.KnockoutJogoId);
        e.Property(x => x.Fase).HasConversion<int>();

        e.HasOne(x => x.Liga)
            .WithMany(x => x.KnockoutJogos)
            .HasForeignKey(x => x.LigaId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.TimeCasa)
            .WithMany()
            .HasForeignKey(x => x.TimeCasaId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.TimeFora)
            .WithMany()
            .HasForeignKey(x => x.TimeForaId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Vencedor)
            .WithMany()
            .HasForeignKey(x => x.VencedorId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Partida)
            .WithMany()
            .HasForeignKey(x => x.PartidaId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(x => new { x.LigaId, x.Fase }).IsUnique();
    }
}
