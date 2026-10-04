using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PerfilTreinadorConfiguration : IEntityTypeConfiguration<PerfilTreinador>
{
    public void Configure(EntityTypeBuilder<PerfilTreinador> b)
    {
        b.ToTable("PerfisTreinadores");
        b.HasKey(x => x.TreinadorId);
        b.Property(x => x.TreinadorId).ValueGeneratedNever();

        b.Property(x => x.Apelido).HasMaxLength(PerfilTreinador.TamanhoApelido);
        b.Property(x => x.Frase).HasMaxLength(PerfilTreinador.TamanhoFrase);
        b.Property(x => x.Esquema).HasMaxLength(12);
        b.Property(x => x.ContentType).HasMaxLength(40);

        b.HasOne(x => x.Treinador).WithOne().HasForeignKey<PerfilTreinador>(x => x.TreinadorId).OnDelete(DeleteBehavior.Cascade);
    }
}
