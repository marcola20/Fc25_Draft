using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TeamLineupOffensiveInstructionsConfiguration : IEntityTypeConfiguration<TeamLineupOffensiveInstructions>
{
    public void Configure(EntityTypeBuilder<TeamLineupOffensiveInstructions> e)
    {
        e.HasKey(x => x.LineupId);
        e.Property(x => x.OffensiveStyle).IsRequired();
        e.Property(x => x.Playmaker).IsRequired();
        e.Property(x => x.AttackArea).IsRequired();
        e.Property(x => x.Positioning).IsRequired();
        e.Property(x => x.SupportRange).IsRequired();
    }
}
