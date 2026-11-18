using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PedidoArteConfiguration : IEntityTypeConfiguration<PedidoArte>
{
    public void Configure(EntityTypeBuilder<PedidoArte> b)
    {
        b.ToTable("PedidosArte");
        b.HasKey(x => x.PedidoArteId);
        b.Property(x => x.Chave).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Chave).IsUnique();
        b.Property(x => x.Titulo).HasMaxLength(200).IsRequired();
        b.Property(x => x.Erro).HasMaxLength(1000);
        b.HasIndex(x => new { x.Situacao, x.CriadoEmUtc });
    }
}

public class ArtesConfigConfiguration : IEntityTypeConfiguration<ArtesConfig>
{
    public void Configure(EntityTypeBuilder<ArtesConfig> b)
    {
        b.ToTable("ArtesConfig");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.ValorMinimo).HasPrecision(18, 2);
    }
}
