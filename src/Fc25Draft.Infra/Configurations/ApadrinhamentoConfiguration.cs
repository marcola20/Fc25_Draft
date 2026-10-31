using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class ApadrinhamentoConfiguration : IEntityTypeConfiguration<Apadrinhamento>
{
    public void Configure(EntityTypeBuilder<Apadrinhamento> b)
    {
        b.ToTable("Apadrinhamentos");
        b.HasKey(x => x.ApadrinhamentoId);
        b.HasIndex(x => new { x.TeamId, x.Temporada });
        b.HasIndex(x => x.PadrinhoId);
        b.HasIndex(x => x.AfilhadoId);

        b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Padrinho).WithMany().HasForeignKey(x => x.PadrinhoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Afilhado).WithMany().HasForeignKey(x => x.AfilhadoId).OnDelete(DeleteBehavior.Cascade);
    }
}
