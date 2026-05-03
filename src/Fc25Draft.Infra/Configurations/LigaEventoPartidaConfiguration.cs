using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaEventoPartidaConfiguration : IEntityTypeConfiguration<LigaEventoPartida>
{
    public void Configure(EntityTypeBuilder<LigaEventoPartida> e)
    {
        e.HasKey(x => x.EventoId);
        e.Property(x => x.Tipo).HasConversion<int>();
        e.Property(x => x.CriadoEm).IsRequired();

        e.HasOne(x => x.Partida)
            .WithMany(x => x.Eventos)
            .HasForeignKey(x => x.PartidaId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Time)
            .WithMany()
            .HasForeignKey(x => x.TimeId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Jogador)
            .WithMany()
            .HasForeignKey(x => x.JogadorId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Assistente)
            .WithMany()
            .HasForeignKey(x => x.AssistenteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
