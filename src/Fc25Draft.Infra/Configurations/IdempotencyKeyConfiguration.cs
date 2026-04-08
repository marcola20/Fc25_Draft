using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> e)
    {
        e.HasKey(x => x.Key);
        e.Property(x => x.Key).HasMaxLength(200).IsRequired();
        e.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        e.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("IX_IdempotencyKeys_ExpiresAtUtc");
    }
}
