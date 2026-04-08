using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class MarketTransactionConfiguration : IEntityTypeConfiguration<MarketTransaction>
{
    public void Configure(EntityTypeBuilder<MarketTransaction> e)
    {
        e.HasKey(x => x.TransactionId);

        e.Property(x => x.Type).HasConversion<int>().IsRequired();
        e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        e.Property(x => x.PerformedBy).IsRequired().HasMaxLength(120);
        e.Property(x => x.Notes).HasMaxLength(400);
        e.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        e.Property(x => x.RowVersion)
         .HasColumnName("xmin")
         .HasColumnType("xid")
         .IsConcurrencyToken()
         .ValueGeneratedOnAddOrUpdate();

        e.HasIndex(x => x.ItemId).HasDatabaseName("IX_MarketTransactions_Item");
        e.HasIndex(x => x.CycleId).HasDatabaseName("IX_MarketTransactions_Cycle");
        e.HasIndex(x => x.PlayerId).HasDatabaseName("IX_MarketTransactions_Player");
        e.HasIndex(x => new { x.ItemId, x.Type, x.CreatedAtUtc }).HasDatabaseName("IX_MarketTransactions_Item_Type_CreatedAt");
        e.HasIndex(x => new { x.CycleId, x.Type, x.CreatedAtUtc }).HasDatabaseName("IX_MarketTransactions_Cycle_Type_CreatedAt");
        e.HasIndex(x => new { x.PlayerId, x.CreatedAtUtc }).HasDatabaseName("IX_MarketTransactions_Player_CreatedAt");
    }
}
