using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class HallDaFamaJogadorConfiguration : IEntityTypeConfiguration<HallDaFamaJogador>
{
    public void Configure(EntityTypeBuilder<HallDaFamaJogador> e)
    {
        e.ToTable("HallDaFamaJogadores");
        e.HasKey(x => x.HallDaFamaJogadorId);
        e.Property(x => x.Motivo).HasMaxLength(300);
        e.Property(x => x.CriadoEm).IsRequired();
        e.HasIndex(x => x.PlayerId).IsUnique();
        e.HasIndex(x => new { x.Temporada, x.Colocacao }).IsUnique();
        e.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
    }
}
