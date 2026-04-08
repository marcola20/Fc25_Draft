using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> e)
    {
        e.HasKey(x => x.PlayerId);
        e.Property(x => x.Name).IsRequired().HasMaxLength(80);
        e.Property(x => x.Overall).IsRequired();
        e.Property(x => x.Age);
        e.Property(x => x.PlayerGuid).IsRequired();

        e.HasOne(x => x.Position)
         .WithMany(p => p.Players)
         .HasForeignKey(x => x.PositionId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(p => p.CurrentTeam)
         .WithMany()
         .HasForeignKey(p => p.CurrentTeamId)
         .OnDelete(DeleteBehavior.SetNull);

        e.HasIndex(x => new { x.Name, x.PositionId });
        e.HasIndex(x => x.PlayerGuid).IsUnique();
    }
}
