using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class DraftRoundConfiguration : IEntityTypeConfiguration<DraftRound>
{
    public void Configure(EntityTypeBuilder<DraftRound> e)
    {
        e.HasKey(x => new { x.DraftId, x.RoundNumber });

        e.HasOne(x => x.Draft)
         .WithMany(d => d.Rounds)
         .HasForeignKey(x => x.DraftId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
