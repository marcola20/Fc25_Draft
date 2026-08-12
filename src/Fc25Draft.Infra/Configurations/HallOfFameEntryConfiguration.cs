using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class HallOfFameEntryConfiguration : IEntityTypeConfiguration<HallOfFameEntry>
{
    public void Configure(EntityTypeBuilder<HallOfFameEntry> e)
    {
        e.HasKey(x => x.HallOfFameId);
        e.Property(x => x.Descricao).IsRequired().HasMaxLength(200);
        e.Property(x => x.Tipo).HasConversion<int>().HasDefaultValue(TipoCompetition.Liga);
        e.Property(x => x.TimeCampeao).IsRequired().HasMaxLength(120);
        e.Property(x => x.Tecnico).HasMaxLength(120);
        e.Property(x => x.Temporada).HasMaxLength(60);
        e.Property(x => x.CriadoEm).IsRequired();
        e.Property(x => x.AtualizadoEm).IsRequired();
    }
}
