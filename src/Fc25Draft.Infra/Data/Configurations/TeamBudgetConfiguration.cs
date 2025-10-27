using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class TeamBudgetConfiguration : IEntityTypeConfiguration<TeamBudget>
{
    public void Configure(EntityTypeBuilder<TeamBudget> builder)
    {
        builder.HasKey(x => x.TeamId);

        builder.Property(x => x.Saldo).HasColumnType("decimal(18,2)").IsRequired();

        builder.HasOne(x => x.Team)
            .WithOne()
            .HasForeignKey<TeamBudget>(x => x.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
