using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.HasKey(x => x.BidId);

        builder.Property(x => x.Valor).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.DataUtc).IsRequired();

        builder.HasIndex(x => new { x.MarketItemId, x.DataUtc })
            .HasDatabaseName("IX_Bid_MarketItemId_DataUtc");

        builder.HasOne(x => x.MarketItem)
            .WithMany(i => i.Bids)
            .HasForeignKey(x => x.MarketItemId);

        builder.HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
