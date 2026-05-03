using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class LigaPunicaoConfiguration : IEntityTypeConfiguration<LigaPunicao>
{
    public void Configure(EntityTypeBuilder<LigaPunicao> e)
    {
        e.HasKey(x => x.PunicaoId);
        e.Property(x => x.Motivo).IsRequired().HasMaxLength(300);
        e.Property(x => x.CriadaEm).IsRequired();

        e.HasOne(x => x.Liga)
            .WithMany(x => x.Punicoes)
            .HasForeignKey(x => x.LigaId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Time)
            .WithMany()
            .HasForeignKey(x => x.TimeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
