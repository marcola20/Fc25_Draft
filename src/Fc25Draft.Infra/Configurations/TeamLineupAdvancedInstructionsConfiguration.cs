using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TeamLineupAdvancedInstructionsConfiguration : IEntityTypeConfiguration<TeamLineupAdvancedInstructions>
{
    public void Configure(EntityTypeBuilder<TeamLineupAdvancedInstructions> e)
    {
        e.HasKey(x => x.LineupId);

        e.Property(x => x.Attack1).IsRequired();
        e.Property(x => x.Attack2).IsRequired();
        e.Property(x => x.Defense1).IsRequired();
        e.Property(x => x.Defense2).IsRequired();

        e.HasOne(x => x.AttackPlayer1)
            .WithMany()
            .HasForeignKey(x => x.AttackPlayer1Id)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.AttackPlayer2)
            .WithMany()
            .HasForeignKey(x => x.AttackPlayer2Id)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.DefensePlayer1)
            .WithMany()
            .HasForeignKey(x => x.DefensePlayer1Id)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.DefensePlayer2)
            .WithMany()
            .HasForeignKey(x => x.DefensePlayer2Id)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
