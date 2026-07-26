using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaTimeConfiguration : IEntityTypeConfiguration<LigaTime>
{
    public void Configure(EntityTypeBuilder<LigaTime> b)
    {
        b.ToTable("LigaTimes");
        b.HasKey(x => x.Id);
        b.HasIndex(x => new { x.LigaId, x.TimeId }).IsUnique();

        b.HasOne(x => x.Liga)
            .WithMany(l => l.Times)
            .HasForeignKey(x => x.LigaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Time)
            .WithMany()
            .HasForeignKey(x => x.TimeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
