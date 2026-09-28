using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PartidaChatMensagemConfiguration : IEntityTypeConfiguration<PartidaChatMensagem>
{
    public const int TamanhoMaximo = 300;

    public void Configure(EntityTypeBuilder<PartidaChatMensagem> b)
    {
        b.ToTable("PartidaChatMensagens");
        b.HasKey(x => x.MensagemId);
        b.Property(x => x.Texto).HasMaxLength(TamanhoMaximo).IsRequired();

        b.HasIndex(x => new { x.PartidaId, x.EnviadaEm });

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
