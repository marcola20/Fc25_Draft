using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TransferHistoryConfiguration : IEntityTypeConfiguration<TransferHistory>
{
    public void Configure(EntityTypeBuilder<TransferHistory> builder)
    {
        builder.HasKey(x => x.TransferId);

        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        builder.Property(x => x.Notes).HasMaxLength(400);
        builder.Property(x => x.PerformedBy).HasMaxLength(120);
        builder.Property(x => x.PerformedAtUtc).IsRequired();

        builder.HasIndex(x => x.PerformedAtUtc);
        builder.HasIndex(x => new { x.PlayerId, x.PerformedAtUtc });
        builder.HasIndex(x => x.FromTeamId);
        builder.HasIndex(x => x.ToTeamId);

        builder.HasOne(x => x.Player)
            .WithMany()
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FromTeam)
            .WithMany()
            .HasForeignKey(x => x.FromTeamId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ToTeam)
            .WithMany()
            .HasForeignKey(x => x.ToTeamId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
