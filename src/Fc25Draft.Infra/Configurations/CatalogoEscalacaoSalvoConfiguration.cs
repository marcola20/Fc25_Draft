using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class CatalogoEscalacaoSalvoConfiguration : IEntityTypeConfiguration<CatalogoEscalacaoSalvo>
{
    public void Configure(EntityTypeBuilder<CatalogoEscalacaoSalvo> b)
    {
        b.ToTable("CatalogoEscalacao");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Json).HasColumnType("jsonb").IsRequired();
    }
}
