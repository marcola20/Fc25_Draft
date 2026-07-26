using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PricingConfigConfiguration : IEntityTypeConfiguration<PricingConfig>
{
    public void Configure(EntityTypeBuilder<PricingConfig> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();

        e.Property(x => x.BaseScale).HasColumnType("numeric");
        e.Property(x => x.OverallBase).HasColumnType("numeric");
        e.Property(x => x.BuyNowFactor).HasColumnType("numeric");
        e.Property(x => x.MinIncrementRate).HasColumnType("numeric");
        e.Property(x => x.MinIncrementStep).HasColumnType("numeric");

        e.Property(x => x.AgeFactorUpTo22).HasColumnType("numeric");
        e.Property(x => x.AgeFactor23To24).HasColumnType("numeric");
        e.Property(x => x.AgeFactor25To26).HasColumnType("numeric");
        e.Property(x => x.AgeFactor27To28).HasColumnType("numeric");
        e.Property(x => x.AgeFactor29To30).HasColumnType("numeric");
        e.Property(x => x.AgeFactor31To32).HasColumnType("numeric");
        e.Property(x => x.AgeFactor33To34).HasColumnType("numeric");
        e.Property(x => x.AgeFactor35Plus).HasColumnType("numeric");
    }
}
