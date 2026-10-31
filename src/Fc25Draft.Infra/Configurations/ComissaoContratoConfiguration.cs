using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class ComissaoContratoConfiguration : IEntityTypeConfiguration<ComissaoContrato>
{
    public void Configure(EntityTypeBuilder<ComissaoContrato> b)
    {
        b.ToTable("ComissaoContratos");
        b.HasKey(x => x.ContratoId);

        // Cada cargo uma vez por time e temporada.
        b.HasIndex(x => new { x.TeamId, x.Temporada, x.Cargo }).IsUnique();

        b.Property(x => x.Cargo).HasConversion<short>();
        b.Property(x => x.Valor).HasColumnType("numeric(18,2)");

        b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
    }
}
