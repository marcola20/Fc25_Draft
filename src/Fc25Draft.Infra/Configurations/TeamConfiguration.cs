using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> e)
    {
        e.HasKey(x => x.TeamId);
        e.Property(x => x.TeamName).IsRequired().HasMaxLength(80);
        e.Property(x => x.OwnerName).HasMaxLength(80);
        e.Property(x => x.Token).IsRequired().HasMaxLength(80);
        e.Property(x => x.Budget).HasColumnType("numeric(18,2)").HasDefaultValue(0m);
        e.Property(x => x.BudgetBlocked).HasColumnType("numeric(18,2)").HasDefaultValue(0m);

        e.HasIndex(x => x.TeamName).IsUnique();
        e.HasIndex(x => x.Token).IsUnique();
    }
}
