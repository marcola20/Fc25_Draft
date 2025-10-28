using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class AdminActionsLogConfiguration : IEntityTypeConfiguration<AdminActionsLog>
{
    public void Configure(EntityTypeBuilder<AdminActionsLog> builder)
    {
        builder.HasKey(x => x.ActionId);

        builder.Property(x => x.ActionType)
            .IsRequired();

        builder.Property(x => x.PerformedBy)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.PayloadJson)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.ActionType, x.CreatedAtUtc })
            .HasDatabaseName("IX_AdminActionsLogs_ActionType_CreatedAtUtc");

        builder.ToTable("AdminActionsLog");
    }
}
