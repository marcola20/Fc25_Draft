using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class ClausulaRevendaConfiguration : IEntityTypeConfiguration<ClausulaRevenda>
{
    public void Configure(EntityTypeBuilder<ClausulaRevenda> b)
    {
        b.ToTable("ClausulasRevenda");
        b.HasKey(x => x.ClausulaId);
        b.Property(x => x.Percentual).HasPrecision(5, 2);
        b.Property(x => x.ValorPago).HasPrecision(18, 2);

        b.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Beneficiario).WithMany().HasForeignKey(x => x.BeneficiarioTeamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Devedor).WithMany().HasForeignKey(x => x.DevedorTeamId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.PlayerId, x.EncerradaEm });
    }
}
