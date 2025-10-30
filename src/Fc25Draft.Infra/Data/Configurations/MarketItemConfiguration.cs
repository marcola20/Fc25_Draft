using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Fc25Draft.Infra.Data.Configurations;

public class MarketItemConfiguration : IEntityTypeConfiguration<MarketItem>
{
    public void Configure(EntityTypeBuilder<MarketItem> builder)
    {
        builder.HasKey(x => x.ItemId);
        builder.Property(x => x.BasePrice).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.BuyNowPrice).HasColumnType("numeric(18,2)").IsRequired(false);
        builder.Property(x => x.MinIncrement).HasColumnType("numeric(18,2)").IsRequired();
        builder.Property(x => x.CurrentLeaderAmount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.PublishedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastUpdateUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(x => x.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .UseXmin();

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
        builder.HasIndex(x => new { x.CycleId, x.PlayerId }).IsUnique();
        builder.HasIndex(x => x.PlayerId).HasDatabaseName("IX_MarketItems_Player");
    }
}
