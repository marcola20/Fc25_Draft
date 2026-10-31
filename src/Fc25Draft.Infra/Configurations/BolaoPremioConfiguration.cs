using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class BolaoPremioConfiguration : IEntityTypeConfiguration<BolaoPremio>
{
    public void Configure(EntityTypeBuilder<BolaoPremio> b)
    {
        b.ToTable("BolaoPremios");
        b.HasKey(x => x.PremioId);

        b.Property(x => x.Chave).HasMaxLength(100).IsRequired();
        b.HasIndex(x => x.Chave).IsUnique();
        b.HasIndex(x => x.Temporada);

        b.Property(x => x.Valor).HasColumnType("numeric(18,2)");
        b.Property(x => x.Motivo).HasMaxLength(160).IsRequired();

        // O histórico do prêmio fica mesmo se a pessoa ou o clube saírem da liga.
        b.HasOne(x => x.Treinador)
            .WithMany()
            .HasForeignKey(x => x.TreinadorId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasOne(x => x.Time)
            .WithMany()
            .HasForeignKey(x => x.TimeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
