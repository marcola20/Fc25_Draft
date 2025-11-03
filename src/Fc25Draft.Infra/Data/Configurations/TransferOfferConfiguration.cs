using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Fc25Draft.Infra.Data.Configurations;

public class TransferOfferConfiguration : IEntityTypeConfiguration<TransferOffer>
{
    public void Configure(EntityTypeBuilder<TransferOffer> builder)
    {
        builder.HasKey(x => x.OfferId);

        builder.Property(x => x.OfferedFee)
            .HasColumnType("numeric(18,2)");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.RespondedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ExpiresAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Message)
            .HasMaxLength(400);

        builder.Property(x => x.ResponseMessage)
            .HasMaxLength(400);

        builder.Property(x => x.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(x => x.Player)
            .WithMany()
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(x => x.FromTeam)
            .WithMany()
            .HasForeignKey(x => x.FromTeamId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(x => x.ToTeam)
            .WithMany()
            .HasForeignKey(x => x.ToTeamId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(x => new { x.PlayerId, x.Status });
        builder.HasIndex(x => x.FromTeamId);
        builder.HasIndex(x => new { x.ToTeamId, x.Status, x.CreatedAtUtc })
            .HasDatabaseName("IX_TransferOffers_ToTeam_Status_CreatedAtUtc")
            .IsDescending(false, false, true);
    }
}
