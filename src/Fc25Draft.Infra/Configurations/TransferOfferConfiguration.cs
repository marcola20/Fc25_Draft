using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TransferOfferConfiguration : IEntityTypeConfiguration<TransferOffer>
{
    public void Configure(EntityTypeBuilder<TransferOffer> builder)
    {
        builder.HasKey(x => x.OfferId);

        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Money).HasColumnType("numeric(18,2)");
        builder.Property(x => x.MoneyPayerTeamId).IsRequired(false);
        builder.Property(x => x.SellOnPercentage).HasColumnType("numeric(5,2)");
        builder.Property(x => x.Clauses).HasMaxLength(2000);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasIndex(x => x.FromTeamId);
        builder.HasIndex(x => x.ToTeamId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedAtUtc);

        builder.HasOne(x => x.FromTeam)
            .WithMany()
            .HasForeignKey(x => x.FromTeamId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ToTeam)
            .WithMany()
            .HasForeignKey(x => x.ToTeamId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ParentOffer)
            .WithMany(x => x.CounterOffers)
            .HasForeignKey(x => x.ParentOfferId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
