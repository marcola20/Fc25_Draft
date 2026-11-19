using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class AbaEscondidaConfiguration : IEntityTypeConfiguration<AbaEscondida>
{
    public void Configure(EntityTypeBuilder<AbaEscondida> b)
    {
        b.ToTable("AbasEscondidas");
        b.HasKey(x => x.Rota);
        b.Property(x => x.Rota).HasMaxLength(200);
    }
}
