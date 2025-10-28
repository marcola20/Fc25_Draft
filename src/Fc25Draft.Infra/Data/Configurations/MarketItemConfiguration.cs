using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class MarketItemConfiguration : IEntityTypeConfiguration<MarketItem>
{
    public void Configure(EntityTypeBuilder<MarketItem> builder)
    {
        builder.HasKey(x => x.ItemId);
        builder.Property(x => x.BasePrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.BuyNowPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.MinIncrement).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.CurrentLeaderAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.LastUpdateUtc).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).IsRequired();

        builder.HasOne(x => x.Player)
            .WithMany(p => p.MarketItems)
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentLeaderTeam)
            .WithMany(t => t.LeadingMarketItems)
            .HasForeignKey(x => x.CurrentLeaderTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WinnerTeam)
            .WithMany(t => t.WonMarketItems)
            .HasForeignKey(x => x.WinnerTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CycleId, x.Status, x.ExpiresAtUtc });
        builder.HasIndex(x => x.PlayerId).HasDatabaseName("IX_MarketItems_Player");
    }
}
