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

        // O padrão é bloqueado: a venda rápida só vale quando o admin libera.
        e.Property(x => x.QuickSellBloqueado).HasDefaultValue(true);
    }
}
