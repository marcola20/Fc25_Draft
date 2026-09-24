using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaEscalacaoPartidaConfiguration : IEntityTypeConfiguration<LigaEscalacaoPartida>
{
    public void Configure(EntityTypeBuilder<LigaEscalacaoPartida> b)
    {
        b.ToTable("LigaEscalacoesPartida");
        b.HasKey(x => x.Id);

        b.HasIndex(x => new { x.PartidaId, x.TimeId, x.JogadorId }).IsUnique();
        b.HasIndex(x => x.JogadorId);

        b.HasOne(x => x.Partida)
            .WithMany(x => x.Escalacoes)
            .HasForeignKey(x => x.PartidaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Time)
            .WithMany()
            .HasForeignKey(x => x.TimeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Jogador)
            .WithMany()
            .HasForeignKey(x => x.JogadorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
