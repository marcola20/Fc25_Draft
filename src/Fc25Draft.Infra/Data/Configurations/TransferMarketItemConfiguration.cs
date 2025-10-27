using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class TransferMarketItemConfiguration : IEntityTypeConfiguration<TransferMarketItem>
{
    public void Configure(EntityTypeBuilder<TransferMarketItem> builder)
    {
        builder.HasKey(x => x.MarketItemId);

        builder.Property(x => x.PrecoBase).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.LanceAtual).HasColumnType("decimal(18,2)");
        builder.Property(x => x.PrecoComprarAgora).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.DataInicioUtc).IsRequired();
        builder.Property(x => x.Status).IsRequired().HasMaxLength(16);

        builder.HasIndex(x => x.Status).HasDatabaseName("IX_TransferMarketItem_Status");
        builder.HasIndex(x => new { x.PlayerId, x.Status }).HasDatabaseName("IX_TransferMarketItem_Player_Status");
        builder.HasIndex(x => x.PlayerId)
            .HasFilter("[Status] = 'OPEN'")
            .IsUnique()
            .HasDatabaseName("IX_TransferMarketItem_Player_Open");

        builder.HasOne(x => x.Player)
            .WithMany()
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.MaiorLanceTeam)
            .WithMany()
            .HasForeignKey(x => x.MaiorLanceTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VencedorTeam)
            .WithMany()
            .HasForeignKey(x => x.VencedorTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Bids)
            .WithOne(b => b.MarketItem)
            .HasForeignKey(b => b.MarketItemId);

        builder.ToTable(t =>
            t.HasCheckConstraint("CK_TransferMarketItem_Status", "[Status] IN ('OPEN','SOLD','EXPIRED')"));
    }
}
