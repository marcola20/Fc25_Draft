using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaCopaPoteConfiguration : IEntityTypeConfiguration<LigaCopaPote>
{
    public void Configure(EntityTypeBuilder<LigaCopaPote> b)
    {
        b.ToTable("LigaCopaPotes");
        b.HasKey(x => x.Id);

        b.HasIndex(x => new { x.LigaId, x.TimeId }).IsUnique();
        b.HasIndex(x => new { x.LigaId, x.Pote });

        b.HasOne(x => x.Liga)
            .WithMany()
            .HasForeignKey(x => x.LigaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Time)
            .WithMany()
            .HasForeignKey(x => x.TimeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
