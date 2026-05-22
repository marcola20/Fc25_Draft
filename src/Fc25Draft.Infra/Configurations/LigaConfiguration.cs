using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaConfiguration : IEntityTypeConfiguration<Liga>
{
    public void Configure(EntityTypeBuilder<Liga> e)
    {
        e.HasKey(x => x.LigaId);
        e.Property(x => x.Nome).IsRequired().HasMaxLength(120);
        e.Property(x => x.TotalRodadas).HasDefaultValue(8);
        e.Property(x => x.Status).HasConversion<int>();
        e.Property(x => x.Tipo).HasConversion<int>().HasDefaultValue(TipoCompetition.Liga);
        e.Property(x => x.CriadoEm).IsRequired();
        e.Property(x => x.AtualizadoEm).IsRequired();
    }
}
