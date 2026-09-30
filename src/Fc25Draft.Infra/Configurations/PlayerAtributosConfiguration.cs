using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PlayerAtributosConfiguration : IEntityTypeConfiguration<PlayerAtributos>
{
    public void Configure(EntityTypeBuilder<PlayerAtributos> e)
    {
        e.ToTable("PlayerAtributos");
        e.HasKey(x => x.PlayerId);
        e.Property(x => x.PlayerId).ValueGeneratedNever();
        e.Property(x => x.Posicoes).HasMaxLength(13);

        e.HasOne(x => x.Player)
         .WithOne(p => p.Atributos)
         .HasForeignKey<PlayerAtributos>(x => x.PlayerId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
