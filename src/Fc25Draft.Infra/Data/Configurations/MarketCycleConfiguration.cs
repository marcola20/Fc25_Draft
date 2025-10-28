using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class MarketCycleConfiguration : IEntityTypeConfiguration<MarketCycle>
{
    public void Configure(EntityTypeBuilder<MarketCycle> builder)
    {
        builder.HasKey(x => x.CycleId);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.NextCycleAtUtc).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();

        builder.HasMany(x => x.Items)
            .WithOne(i => i.Cycle)
            .HasForeignKey(i => i.CycleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
