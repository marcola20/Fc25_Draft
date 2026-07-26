using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TransferConfigConfiguration : IEntityTypeConfiguration<TransferConfig>
{
    public void Configure(EntityTypeBuilder<TransferConfig> e)
    {
        e.HasKey(x => x.Id);
        e.Property(x => x.Id).ValueGeneratedNever();
    }
}
