using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PartidaChatMensagemConfiguration : IEntityTypeConfiguration<PartidaChatMensagem>
{
    public const int TamanhoMaximo = 300;

    public void Configure(EntityTypeBuilder<PartidaChatMensagem> b)
    {
        // A mensagem é de um jogo ou de um mosaico, nunca dos dois nem de nenhum.
        b.ToTable("PartidaChatMensagens", t => t.HasCheckConstraint("CK_PartidaChatMensagens_Sala",
            "(\"PartidaId\" IS NULL) <> (\"MosaicoId\" IS NULL)"));
        b.HasKey(x => x.MensagemId);
        b.Property(x => x.Texto).HasMaxLength(TamanhoMaximo).IsRequired();

        b.HasIndex(x => new { x.PartidaId, x.EnviadaEm });
        b.HasIndex(x => new { x.MosaicoId, x.EnviadaEm });

        b.HasOne(x => x.Partida)
            .WithMany()
            .HasForeignKey(x => x.PartidaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Mosaico)
            .WithMany()
            .HasForeignKey(x => x.MosaicoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Treinador)
            .WithMany()
            .HasForeignKey(x => x.TreinadorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
