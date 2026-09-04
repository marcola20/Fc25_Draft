using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class DraftWishlistEntryConfiguration : IEntityTypeConfiguration<DraftWishlistEntry>
{
    public void Configure(EntityTypeBuilder<DraftWishlistEntry> e)
    {
        e.ToTable("DraftWishlistEntries");
        e.HasKey(x => x.DraftWishlistEntryId);
        e.Property(x => x.Ordem).IsRequired();
        e.Property(x => x.CriadoEm).IsRequired();

        e.HasIndex(x => new { x.TeamId, x.PlayerId }).IsUnique();
        e.HasIndex(x => x.TeamId);

        e.HasOne(x => x.Team)
         .WithMany()
         .HasForeignKey(x => x.TeamId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Player)
         .WithMany()
         .HasForeignKey(x => x.PlayerId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
