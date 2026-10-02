using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class FotoJogadorConfiguration : IEntityTypeConfiguration<FotoJogador>
{
    public void Configure(EntityTypeBuilder<FotoJogador> b)
    {
        b.ToTable("FotosJogadores");
        b.HasKey(x => x.PlayerId);
        b.Property(x => x.PlayerId).ValueGeneratedNever();
        b.Property(x => x.Imagem).IsRequired();
        b.Property(x => x.ContentType).HasMaxLength(40).IsRequired();
        b.Property(x => x.Origem).HasMaxLength(10).IsRequired();
        b.HasOne(x => x.Player).WithOne().HasForeignKey<FotoJogador>(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
    }
}
