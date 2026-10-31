using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class DireitoDeRecompraConfiguration : IEntityTypeConfiguration<DireitoDeRecompra>
{
    public void Configure(EntityTypeBuilder<DireitoDeRecompra> b)
    {
        b.ToTable("DireitosDeRecompra");
        b.HasKey(x => x.DireitoId);
        b.HasIndex(x => x.PlayerId);
        b.HasIndex(x => x.VendedorId);
        b.HasIndex(x => x.CompradorId);

        b.Property(x => x.Preco).HasColumnType("numeric(18,2)");

        b.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Vendedor).WithMany().HasForeignKey(x => x.VendedorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Comprador).WithMany().HasForeignKey(x => x.CompradorId).OnDelete(DeleteBehavior.Cascade);
    }
}
