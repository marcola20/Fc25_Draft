using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PremioEstatisticaConfiguration : IEntityTypeConfiguration<PremioEstatistica>
{
    public void Configure(EntityTypeBuilder<PremioEstatistica> b)
    {
        b.ToTable("PremiosEstatistica");
        b.HasKey(x => x.PremioId);
        b.Property(x => x.Valor).HasColumnType("numeric(18,2)");

        b.Property(x => x.Detalhe).HasMaxLength(200);

        // O serviço não deixa dar duas vezes a mesma competição.
        b.HasIndex(x => new { x.LigaId, x.Categoria });
        b.HasIndex(x => new { x.Temporada, x.Categoria });
        b.HasOne(x => x.Liga).WithMany().HasForeignKey(x => x.LigaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.OverPara).WithMany().HasForeignKey(x => x.OverParaPlayerId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Evolucao).WithMany().HasForeignKey(x => x.EvolucaoPesId).OnDelete(DeleteBehavior.SetNull);
    }
}
