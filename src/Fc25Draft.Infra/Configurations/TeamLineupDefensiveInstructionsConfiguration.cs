using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TeamLineupDefensiveInstructionsConfiguration : IEntityTypeConfiguration<TeamLineupDefensiveInstructions>
{
    public void Configure(EntityTypeBuilder<TeamLineupDefensiveInstructions> e)
    {
        e.HasKey(x => x.LineupId);
        e.Property(x => x.DefensiveStyle).IsRequired();
        e.Property(x => x.ContainmentArea).IsRequired();
        e.Property(x => x.Pressure).IsRequired();
        e.Property(x => x.DefensiveLine).IsRequired();
        e.Property(x => x.Density).IsRequired();
    }
}
