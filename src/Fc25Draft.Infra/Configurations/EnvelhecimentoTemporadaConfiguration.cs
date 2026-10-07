using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class EnvelhecimentoTemporadaConfiguration : IEntityTypeConfiguration<EnvelhecimentoTemporada>
{
    public void Configure(EntityTypeBuilder<EnvelhecimentoTemporada> b)
    {
        b.ToTable("EnvelhecimentosTemporada");
        // Uma por temporada: é a trava do "envelhecer".
        b.HasKey(x => x.Temporada);
        b.Property(x => x.Temporada).ValueGeneratedNever();
    }
}
