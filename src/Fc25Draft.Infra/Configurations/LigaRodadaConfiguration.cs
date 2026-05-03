using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaRodadaConfiguration : IEntityTypeConfiguration<LigaRodada>
{
    public void Configure(EntityTypeBuilder<LigaRodada> e)
    {
        e.HasKey(x => x.RodadaId);
        e.Property(x => x.Numero).IsRequired();

        e.HasOne(x => x.Liga)
            .WithMany(x => x.Rodadas)
            .HasForeignKey(x => x.LigaId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(x => new { x.LigaId, x.Numero }).IsUnique();
    }
}
