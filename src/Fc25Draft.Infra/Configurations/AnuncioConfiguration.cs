using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class AnuncioConfiguration : IEntityTypeConfiguration<Anuncio>
{
    public void Configure(EntityTypeBuilder<Anuncio> b)
    {
        b.ToTable("Anuncios");
        b.HasKey(x => x.AnuncioId);

        b.Property(x => x.Grupo).HasMaxLength(80).IsRequired();
        b.Property(x => x.Titulo).HasMaxLength(160).IsRequired();
        b.Property(x => x.Texto).IsRequired();
        b.Property(x => x.Explicacao).IsRequired();
        b.Property(x => x.Resumo).HasMaxLength(300).IsRequired().HasDefaultValue("");
        b.Property(x => x.TextoSite).IsRequired().HasDefaultValue("");
        b.HasIndex(x => x.PublicadoEm);
    }
}
