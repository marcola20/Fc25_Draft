using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaGrupoTimeConfiguration : IEntityTypeConfiguration<LigaGrupoTime>
{
    public void Configure(EntityTypeBuilder<LigaGrupoTime> b)
    {
        b.ToTable("LigaGruposTimes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Grupo).HasConversion<int>().IsRequired();

        b.HasIndex(x => new { x.LigaId, x.TimeId }).IsUnique();
        b.HasIndex(x => new { x.LigaId, x.Grupo });

        b.HasOne(x => x.Liga)
            .WithMany(l => l.Grupos)
            .HasForeignKey(x => x.LigaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Time)
            .WithMany()
            .HasForeignKey(x => x.TimeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
