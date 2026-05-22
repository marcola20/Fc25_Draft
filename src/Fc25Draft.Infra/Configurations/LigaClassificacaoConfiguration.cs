using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaClassificacaoConfiguration : IEntityTypeConfiguration<LigaClassificacao>
{
    public void Configure(EntityTypeBuilder<LigaClassificacao> e)
    {
        e.HasKey(x => x.ClassificacaoId);

        e.HasOne(x => x.Liga)
            .WithMany(x => x.Classificacoes)
            .HasForeignKey(x => x.LigaId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Time)
            .WithMany()
            .HasForeignKey(x => x.TimeId)
            .OnDelete(DeleteBehavior.Restrict);

        e.Property(x => x.Grupo).HasConversion<int?>().IsRequired(false);

        e.HasIndex(x => new { x.LigaId, x.TimeId }).IsUnique();
    }
}
