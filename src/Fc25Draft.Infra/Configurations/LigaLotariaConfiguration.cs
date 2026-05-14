using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaLotariaConfiguration : IEntityTypeConfiguration<LigaLoteria>
{
    public void Configure(EntityTypeBuilder<LigaLoteria> b)
    {
        b.ToTable("LigaLoterias");
        b.HasKey(x => x.LoteraiaId);
        b.Property(x => x.LigaNome).IsRequired().HasMaxLength(200);
        b.Property(x => x.CriadoEm).IsRequired();
        b.HasMany(x => x.Picks)
         .WithOne(x => x.Loteria)
         .HasForeignKey(x => x.LoteraiaId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LigaLotariaPickConfiguration : IEntityTypeConfiguration<LigaLoteriaPick>
{
    public void Configure(EntityTypeBuilder<LigaLoteriaPick> b)
    {
        b.ToTable("LigaLoteriaPicks");
        b.HasKey(x => x.PickId);
        b.Property(x => x.TimeNome).IsRequired().HasMaxLength(200);
    }
}
