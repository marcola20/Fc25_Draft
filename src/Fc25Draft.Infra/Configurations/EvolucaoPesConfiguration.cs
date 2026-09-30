using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class EvolucaoPesConfiguration : IEntityTypeConfiguration<EvolucaoPes>
{
    public void Configure(EntityTypeBuilder<EvolucaoPes> e)
    {
        e.ToTable("EvolucoesPes");
        e.HasKey(x => x.EvolucaoPesId);
        e.Property(x => x.Motivo).HasMaxLength(60).IsRequired();
        e.Property(x => x.Mudancas).HasMaxLength(200).IsRequired();
        e.HasIndex(x => x.AplicadaNoJogoEmUtc);

        e.HasOne(x => x.Player)
         .WithMany()
         .HasForeignKey(x => x.PlayerId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
