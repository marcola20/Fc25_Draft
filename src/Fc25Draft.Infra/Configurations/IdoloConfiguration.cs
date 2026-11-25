using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class IdoloConfiguration : IEntityTypeConfiguration<Idolo>
{
    public void Configure(EntityTypeBuilder<Idolo> b)
    {
        b.ToTable("Idolos");
        b.HasKey(x => x.IdoloId);

        // Ídolo de um clube uma vez só.
        b.HasIndex(x => new { x.JogadorId, x.TimeId }).IsUnique();
        b.HasIndex(x => x.TimeId);

        b.Property(x => x.Motivos).HasMaxLength(500).IsRequired();

        b.HasOne(x => x.Jogador).WithMany().HasForeignKey(x => x.JogadorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.Cascade);
    }
}
