using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class AberturaTemporadaConfiguration : IEntityTypeConfiguration<AberturaTemporada>
{
    public void Configure(EntityTypeBuilder<AberturaTemporada> b)
    {
        b.ToTable("AberturasTemporada");
        b.HasKey(x => x.Temporada);
        b.Property(x => x.Temporada).ValueGeneratedNever();
    }
}
