using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TeamLineupSlotConfiguration : IEntityTypeConfiguration<TeamLineupSlot>
{
    public void Configure(EntityTypeBuilder<TeamLineupSlot> e)
    {
        e.HasKey(x => x.LineupSlotId);
        e.Property(x => x.SlotCode).IsRequired().HasMaxLength(20);
        e.Property(x => x.DisplayName).IsRequired().HasMaxLength(80);
        e.Property(x => x.IsBench).IsRequired();
        e.Property(x => x.Order).IsRequired();

        e.HasIndex(x => new { x.LineupId, x.SlotCode }).IsUnique();

        e.HasOne(x => x.Lineup)
         .WithMany(l => l.Slots)
         .HasForeignKey(x => x.LineupId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Player)
         .WithMany()
         .HasForeignKey(x => x.PlayerId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
