using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class ResultadoPesPendenteConfiguration : IEntityTypeConfiguration<ResultadoPesPendente>
{
    public void Configure(EntityTypeBuilder<ResultadoPesPendente> b)
    {
        b.ToTable("ResultadosPesPendentes");
        b.HasKey(x => x.Id);

        b.Property(x => x.Video).HasMaxLength(500);
        b.Property(x => x.Json).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.Competicao).HasMaxLength(200);
        b.Property(x => x.Onde).HasMaxLength(200);
        b.Property(x => x.Casa).HasMaxLength(200).IsRequired();
        b.Property(x => x.Fora).HasMaxLength(200).IsRequired();
    }
}

public class ResultadoPesConfigConfiguration : IEntityTypeConfiguration<ResultadoPesConfig>
{
    public void Configure(EntityTypeBuilder<ResultadoPesConfig> b)
    {
        b.ToTable("ResultadosPesConfig");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
    }
}
