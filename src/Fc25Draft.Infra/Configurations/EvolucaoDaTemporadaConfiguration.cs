using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class EvolucaoDaTemporadaConfiguration : IEntityTypeConfiguration<EvolucaoDaTemporada>
{
    public void Configure(EntityTypeBuilder<EvolucaoDaTemporada> b)
    {
        b.ToTable("EvolucoesDaTemporada");
        // Uma por temporada: é a trava da evolução.
        b.HasKey(x => x.Temporada);
        b.Property(x => x.Temporada).ValueGeneratedNever();
    }
}

public class VariacaoDaTemporadaConfiguration : IEntityTypeConfiguration<VariacaoDaTemporada>
{
    public void Configure(EntityTypeBuilder<VariacaoDaTemporada> b)
    {
        b.ToTable("VariacoesDaTemporada");
        b.HasKey(x => x.Id);

        b.Property(x => x.NotaMedia).HasColumnType("numeric(4,2)");
        b.Property(x => x.Explicacao).HasMaxLength(200).IsRequired();

        b.HasIndex(x => new { x.Temporada, x.PlayerId }).IsUnique();
        b.HasIndex(x => x.PlayerId);

        b.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
    }
}
