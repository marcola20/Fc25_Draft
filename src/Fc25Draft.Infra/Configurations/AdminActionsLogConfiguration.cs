using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class AdminActionsLogConfiguration : IEntityTypeConfiguration<AdminActionsLog>
{
    public void Configure(EntityTypeBuilder<AdminActionsLog> e)
    {
        e.ToTable("AdminActionsLog");
        e.HasKey(x => x.ActionId);

        e.Property(x => x.ActionId).ValueGeneratedNever();
        e.Property(x => x.ActionType).IsRequired();
        e.Property(x => x.PerformedBy).IsRequired().HasMaxLength(120);
        e.Property(x => x.PayloadJson).IsRequired();
        e.Property(x => x.CreatedAtUtc).IsRequired();

        e.HasIndex(x => new { x.ActionType, x.CreatedAtUtc })
         .IsDescending(false, true)
         .HasDatabaseName("IX_AdminActionsLog_ActionType_CreatedAtUtc");
    }
}
