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

        e.Property(x => x.AskingPrice).HasColumnType("numeric(18,2)");
        e.Property(x => x.VariacaoCongelada).HasColumnType("numeric(6,4)");
        e.Property(x => x.Papel).HasConversion<short>();
        e.HasIndex(x => new { x.TeamId, x.NumeroCamisa })
         .IsUnique()
         .HasFilter("\"NumeroCamisa\" IS NOT NULL");
        e.ToTable(t => t.HasCheckConstraint("CK_TeamRosters_NumeroCamisa", "\"NumeroCamisa\" BETWEEN 1 AND 99"));

        e.HasOne(x => x.Team)
         .WithMany(t => t.Roster)
         .HasForeignKey(x => x.TeamId);

        e.HasOne(x => x.Player)
         .WithMany(p => p.TeamRosters)
         .HasForeignKey(x => x.PlayerId);
    }
}
