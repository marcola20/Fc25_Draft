using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TreinadorConfiguration : IEntityTypeConfiguration<Treinador>
{
    public void Configure(EntityTypeBuilder<Treinador> b)
    {
        b.ToTable("Treinadores");
        b.HasKey(x => x.TreinadorId);

        b.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        b.Property(x => x.Token).HasMaxLength(80).IsRequired();
        b.HasIndex(x => x.Token).IsUnique();

        b.HasMany(x => x.Passagens)
            .WithOne(x => x.Treinador)
            .HasForeignKey(x => x.TreinadorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class TreinadorPassagemConfiguration : IEntityTypeConfiguration<TreinadorPassagem>
{
    public void Configure(EntityTypeBuilder<TreinadorPassagem> b)
    {
        b.ToTable("TreinadorPassagens");
        b.HasKey(x => x.PassagemId);

        b.HasIndex(x => new { x.TreinadorId, x.Desde });
        b.HasIndex(x => new { x.TimeId, x.Papel });

        b.HasOne(x => x.Time)
            .WithMany()
            .HasForeignKey(x => x.TimeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
