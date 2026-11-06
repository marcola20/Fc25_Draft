using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class JogosAnotadosConfiguration : IEntityTypeConfiguration<JogosAnotados>
{
    public void Configure(EntityTypeBuilder<JogosAnotados> b)
    {
        b.ToTable("LigaJogosAnotados");
        b.HasKey(x => new { x.JogadorId, x.LigaId, x.TimeId });

        b.HasOne(x => x.Jogador).WithMany().HasForeignKey(x => x.JogadorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Liga).WithMany().HasForeignKey(x => x.LigaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.LigaId);
    }
}
