using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class TransferHistoryConfiguration : IEntityTypeConfiguration<TransferHistory>
{
    public void Configure(EntityTypeBuilder<TransferHistory> builder)
    {
        builder.HasKey(x => x.TransferHistoryId);

        builder.Property(x => x.Valor).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(x => x.Tipo).IsRequired().HasMaxLength(20);
        builder.Property(x => x.DataUtc).IsRequired();

        builder.HasIndex(x => x.DataUtc)
            .HasDatabaseName("IX_TransferHistory_DataUtc");

        builder.HasOne(x => x.Player)
            .WithMany()
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.OrigemTeam)
            .WithMany()
            .HasForeignKey(x => x.OrigemTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DestinoTeam)
            .WithMany()
            .HasForeignKey(x => x.DestinoTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
            t.HasCheckConstraint("CK_TransferHistory_Tipo", "[Tipo] IN ('MARKET_AUCTION','TEAM_SALE','TEAM_TRADE')"));
    }
}
