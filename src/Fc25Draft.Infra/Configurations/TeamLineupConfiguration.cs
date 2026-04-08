using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TeamLineupConfiguration : IEntityTypeConfiguration<TeamLineup>
{
    public void Configure(EntityTypeBuilder<TeamLineup> e)
    {
        e.HasKey(x => x.LineupId);
        e.Property(x => x.Name).IsRequired().HasMaxLength(80);
        e.Property(x => x.Formation).IsRequired().HasMaxLength(20);
        e.Property(x => x.TacticCode).HasMaxLength(40);
        e.Property(x => x.CreatedAt).IsRequired();
        e.Property(x => x.UpdatedAt).IsRequired();

        e.HasIndex(x => new { x.TeamId, x.IsActive })
         .HasDatabaseName("IX_Lineup_Team_Active");

        e.HasOne(x => x.Team)
         .WithMany(t => t.Lineups)
         .HasForeignKey(x => x.TeamId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.CaptainPlayer)
         .WithMany()
         .HasForeignKey(x => x.CaptainPlayerId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.ShortFreeKickLeftPlayer)
         .WithMany()
         .HasForeignKey(x => x.ShortFreeKickLeftPlayerId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.ShortFreeKickRightPlayer)
         .WithMany()
         .HasForeignKey(x => x.ShortFreeKickRightPlayerId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.LongFreeKickPlayer)
         .WithMany()
         .HasForeignKey(x => x.LongFreeKickPlayerId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.PenaltiesPlayer)
         .WithMany()
         .HasForeignKey(x => x.PenaltiesPlayerId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.CornerLeftPlayer)
         .WithMany()
         .HasForeignKey(x => x.CornerLeftPlayerId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.CornerRightPlayer)
         .WithMany()
         .HasForeignKey(x => x.CornerRightPlayerId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}
