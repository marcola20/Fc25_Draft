using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TeamRosterConfiguration : IEntityTypeConfiguration<TeamRoster>
{
    public void Configure(EntityTypeBuilder<TeamRoster> e)
    {
        e.HasKey(x => new { x.TeamId, x.PlayerId });
        e.HasIndex(x => x.PlayerId).IsUnique();

        e.HasOne(x => x.Team)
         .WithMany(t => t.Roster)
         .HasForeignKey(x => x.TeamId);

        e.HasOne(x => x.Player)
         .WithMany(p => p.TeamRosters)
         .HasForeignKey(x => x.PlayerId);
    }
}
