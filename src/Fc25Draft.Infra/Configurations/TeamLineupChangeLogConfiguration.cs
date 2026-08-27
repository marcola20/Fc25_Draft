using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TeamLineupChangeLogConfiguration : IEntityTypeConfiguration<TeamLineupChangeLog>
{
    public void Configure(EntityTypeBuilder<TeamLineupChangeLog> e)
    {
        e.HasKey(x => x.ChangeLogId);
        e.Property(x => x.ChangesJson).IsRequired();
        e.Property(x => x.ChangedAtUtc).IsRequired();

        e.HasIndex(x => new { x.LineupId, x.ChangedAtUtc })
         .HasDatabaseName("IX_LineupChangeLog_Lineup_ChangedAt");

        e.HasOne(x => x.Lineup)
         .WithMany()
         .HasForeignKey(x => x.LineupId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
