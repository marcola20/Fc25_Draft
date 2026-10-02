using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PartidaReacaoConfiguration : IEntityTypeConfiguration<PartidaReacao>
{
    public void Configure(EntityTypeBuilder<PartidaReacao> b)
    {
        b.ToTable("PartidaReacoes");
        b.HasKey(x => new { x.PartidaId, x.TreinadorId, x.Emoji });
        b.Property(x => x.Emoji).HasMaxLength(16).IsRequired();

        b.HasOne(x => x.Partida)
            .WithMany()
            .HasForeignKey(x => x.PartidaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Treinador)
            .WithMany()
            .HasForeignKey(x => x.TreinadorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
