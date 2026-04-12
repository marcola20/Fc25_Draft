using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TransferOfferPlayerConfiguration : IEntityTypeConfiguration<TransferOfferPlayer>
{
    public void Configure(EntityTypeBuilder<TransferOfferPlayer> builder)
    {
        builder.HasKey(x => new { x.OfferId, x.PlayerId });

        builder.Property(x => x.IsTarget).IsRequired();

        builder.HasOne(x => x.Offer)
            .WithMany(x => x.Players)
            .HasForeignKey(x => x.OfferId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Player)
            .WithMany()
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
