using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TreinoSemanalConfiguration : IEntityTypeConfiguration<TreinoSemanal>
{
    public void Configure(EntityTypeBuilder<TreinoSemanal> b)
    {
        b.ToTable("TreinosSemanais");
        b.HasKey(x => x.TreinoId);

        // Cada jogador uma vez por semana em cada time.
        b.HasIndex(x => new { x.TeamId, x.Semana, x.PlayerId }).IsUnique();
        b.HasIndex(x => x.PlayerId);
        b.HasIndex(x => x.AplicadoEm);

        b.Property(x => x.Foco).HasConversion<short>();
        b.Property(x => x.Atributo).HasMaxLength(40);
        b.Property(x => x.SemEfeito).HasMaxLength(120);

        b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
    }
}
