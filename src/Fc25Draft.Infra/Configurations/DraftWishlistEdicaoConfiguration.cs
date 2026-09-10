using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class DraftWishlistEdicaoConfiguration : IEntityTypeConfiguration<DraftWishlistEdicao>
{
    public void Configure(EntityTypeBuilder<DraftWishlistEdicao> e)
    {
        e.ToTable("DraftWishlistEdicoes");
        e.HasKey(x => x.Numero);
        e.Property(x => x.Numero).ValueGeneratedNever();
        e.Property(x => x.Nome).IsRequired().HasMaxLength(80);
        e.Property(x => x.CriadoEm).IsRequired();
        e.Ignore(x => x.Aberta);
    }
}
