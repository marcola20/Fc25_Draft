using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class TransferOfferSwapPlayerConfiguration : IEntityTypeConfiguration<TransferOfferSwapPlayer>
{
    public void Configure(EntityTypeBuilder<TransferOfferSwapPlayer> builder)
    {
        builder.HasKey(x => x.SwapPlayerId);

        builder.HasOne(x => x.Offer)
            .WithMany(o => o.SwapPlayers)
            .HasForeignKey(x => x.OfferId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired();

        builder.HasOne(x => x.Player)
            .WithMany()
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(x => new { x.OfferId, x.PlayerId })
            .IsUnique();

        builder.HasIndex(x => x.TeamId);
    }
}
