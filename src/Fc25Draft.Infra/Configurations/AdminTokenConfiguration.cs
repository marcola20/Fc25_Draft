using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class AdminTokenConfiguration : IEntityTypeConfiguration<AdminToken>
{
    public void Configure(EntityTypeBuilder<AdminToken> e)
    {
        e.HasKey(x => x.AdminTokenId);
        e.Property(x => x.Token).HasMaxLength(500).IsRequired();
        e.Property(x => x.Description).HasMaxLength(500);
        e.Property(x => x.IsActive).IsRequired().HasDefaultValue(true);
        e.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        e.Property(x => x.LastUsedAtUtc).HasColumnType("timestamp with time zone");
        e.Property(x => x.DeactivatedAtUtc).HasColumnType("timestamp with time zone");

        e.HasIndex(x => x.Token).IsUnique().HasDatabaseName("IX_AdminTokens_Token_Unique");
        e.HasIndex(x => x.IsActive).HasDatabaseName("IX_AdminTokens_IsActive");
    }
}
