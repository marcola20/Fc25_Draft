using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class MosaicoConfiguration : IEntityTypeConfiguration<Mosaico>
{
    public const int TamanhoTitulo = 120;

    public void Configure(EntityTypeBuilder<Mosaico> b)
    {
        b.ToTable("Mosaicos");
        b.HasKey(x => x.MosaicoId);
        b.Property(x => x.Titulo).HasMaxLength(TamanhoTitulo).IsRequired();
        b.Property(x => x.YoutubeVideoId).HasMaxLength(20);
        b.HasIndex(x => x.CriadoEm);
    }
}

public class MosaicoRodadaConfiguration : IEntityTypeConfiguration<MosaicoRodada>
{
    public void Configure(EntityTypeBuilder<MosaicoRodada> b)
    {
        b.ToTable("MosaicoRodadas");
        b.HasKey(x => new { x.MosaicoId, x.RodadaId });

        b.HasOne(x => x.Mosaico)
            .WithMany(m => m.Rodadas)
            .HasForeignKey(x => x.MosaicoId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Rodada)
            .WithMany()
            .HasForeignKey(x => x.RodadaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class MosaicoReacaoConfiguration : IEntityTypeConfiguration<MosaicoReacao>
{
    public void Configure(EntityTypeBuilder<MosaicoReacao> b)
    {
        b.ToTable("MosaicoReacoes");
        b.HasKey(x => new { x.MosaicoId, x.TreinadorId, x.Emoji });
        b.Property(x => x.Emoji).HasMaxLength(16).IsRequired();

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
