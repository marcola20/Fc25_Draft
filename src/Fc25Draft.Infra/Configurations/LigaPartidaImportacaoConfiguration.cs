using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaPartidaImportacaoConfiguration : IEntityTypeConfiguration<LigaPartidaImportacao>
{
    public void Configure(EntityTypeBuilder<LigaPartidaImportacao> b)
    {
        b.ToTable("LigaPartidaImportacoes");
        b.HasKey(x => x.PartidaId);

        b.Property(x => x.Video).HasMaxLength(500);
        b.Property(x => x.Json).HasColumnType("jsonb").IsRequired();

        b.HasOne(x => x.Partida)
            .WithOne()
            .HasForeignKey<LigaPartidaImportacao>(x => x.PartidaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
