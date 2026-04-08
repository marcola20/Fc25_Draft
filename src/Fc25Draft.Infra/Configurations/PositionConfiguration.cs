using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> e)
    {
        e.HasKey(x => x.PositionId);

        e.Property(x => x.PositionId)
         .ValueGeneratedNever();

        e.Property(x => x.Name)
         .IsRequired()
         .HasMaxLength(40);

        e.HasIndex(x => x.Name).IsUnique();

        e.HasData(
            new() { PositionId = 1,  Name = "Goleiro" },
            new() { PositionId = 2,  Name = "Zagueiro" },
            new() { PositionId = 3,  Name = "Lateral Esquerdo" },
            new() { PositionId = 4,  Name = "Lateral Direito" },
            new() { PositionId = 5,  Name = "Volante" },
            new() { PositionId = 6,  Name = "Meia de Ligação" },
            new() { PositionId = 7,  Name = "Meia Atacante" },
            new() { PositionId = 8,  Name = "Meia Esquerda" },
            new() { PositionId = 9,  Name = "Ponta Esquerda" },
            new() { PositionId = 10, Name = "Meia Direita" },
            new() { PositionId = 11, Name = "Ponta Direita" },
            new() { PositionId = 12, Name = "Centroavante" },
            new() { PositionId = 13, Name = "Segundo Atacante" }
        );
    }
}
