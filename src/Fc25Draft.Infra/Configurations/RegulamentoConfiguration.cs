using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class RegulamentoConfiguration : IEntityTypeConfiguration<Regulamento>
{
    public void Configure(EntityTypeBuilder<Regulamento> b)
    {
        b.ToTable("Regulamentos");
        b.HasKey(x => x.RegulamentoId);

        b.Property(x => x.Titulo).HasMaxLength(160).IsRequired();
        b.Property(x => x.Conteudo).IsRequired();
        b.HasIndex(x => x.Temporada).IsUnique();
    }
}
