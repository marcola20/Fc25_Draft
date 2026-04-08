using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class DraftPickConfiguration : IEntityTypeConfiguration<DraftPick>
{
    public void Configure(EntityTypeBuilder<DraftPick> e)
    {
        e.HasKey(x => new { x.DraftId, x.OverallPick });

        e.HasIndex(x => new { x.DraftId, x.RoundNumber, x.PickInRound }).IsUnique();
        e.HasIndex(x => new { x.DraftId, x.TeamId, x.RoundNumber });
        e.HasIndex(x => new { x.DraftId, x.PlayerId })
         .IsUnique()
         .HasFilter("\"PlayerId\" IS NOT NULL");

        e.HasOne(x => x.Draft)
         .WithMany(d => d.Picks)
         .HasForeignKey(x => x.DraftId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Round)
         .WithMany(r => r.Picks)
         .HasForeignKey(x => new { x.DraftId, x.RoundNumber })
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Team)
         .WithMany(t => t.DraftPicks)
         .HasForeignKey(x => x.TeamId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.Player)
         .WithMany(pl => pl.DraftPicks)
         .HasForeignKey(x => x.PlayerId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
