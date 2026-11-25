using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaEstatisticaJogadorConfiguration : IEntityTypeConfiguration<LigaEstatisticaJogador>
{
    public void Configure(EntityTypeBuilder<LigaEstatisticaJogador> b)
    {
        b.ToTable("LigaEstatisticasJogadores");
        b.HasKey(x => x.Id);

        b.Property(x => x.NomePes).HasMaxLength(100).IsRequired();
        b.Property(x => x.Posicao).HasMaxLength(5);
        b.Property(x => x.Grupo).HasMaxLength(10);
        b.Property(x => x.NotaPes).HasPrecision(3, 1);
        b.Property(x => x.DistanciaDeDrible).HasPrecision(7, 1);
        b.Property(x => x.VelocidadeMedia).HasPrecision(4, 1);

        b.HasOne(x => x.Partida).WithMany().HasForeignKey(x => x.PartidaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Jogador).WithMany().HasForeignKey(x => x.JogadorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.Restrict);

        // Um jogador do cadastro aparece uma vez por partida (nulos não colidem no Postgres).
        b.HasIndex(x => new { x.PartidaId, x.JogadorId }).IsUnique();
        b.HasIndex(x => x.JogadorId);
    }
}
