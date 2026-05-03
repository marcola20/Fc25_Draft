using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaPartidaConfiguration : IEntityTypeConfiguration<LigaPartida>
{
    public void Configure(EntityTypeBuilder<LigaPartida> e)
    {
        e.HasKey(x => x.PartidaId);
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.GolsCasa).HasDefaultValue(0);
        e.Property(x => x.GolsFora).HasDefaultValue(0);
        e.Property(x => x.IsWO).HasDefaultValue(false);
        e.Property(x => x.TemPenaltis).HasDefaultValue(false);

        e.HasOne(x => x.Rodada)
            .WithMany(x => x.Partidas)
            .HasForeignKey(x => x.RodadaId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.TimeCasa)
            .WithMany()
            .HasForeignKey(x => x.TimeCasaId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.TimeFora)
            .WithMany()
            .HasForeignKey(x => x.TimeForaId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.PenaltisVencedor)
            .WithMany()
            .HasForeignKey(x => x.PenaltisVencedorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
