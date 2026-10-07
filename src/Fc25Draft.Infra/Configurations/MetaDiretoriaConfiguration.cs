using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class MetaDiretoriaConfiguration : IEntityTypeConfiguration<MetaDiretoria>
{
    public void Configure(EntityTypeBuilder<MetaDiretoria> b)
    {
        b.ToTable("MetasDiretoria");
        b.HasKey(x => x.MetaId);

        // Uma meta por time em cada competição.
        b.HasIndex(x => new { x.LigaId, x.TimeId }).IsUnique();
        b.HasIndex(x => x.Temporada);

        b.HasOne(x => x.Liga).WithMany().HasForeignKey(x => x.LigaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DiretoriaPagamentoConfiguration : IEntityTypeConfiguration<DiretoriaPagamento>
{
    public void Configure(EntityTypeBuilder<DiretoriaPagamento> b)
    {
        b.ToTable("DiretoriaPagamentos");
        b.HasKey(x => x.PagamentoId);

        b.Property(x => x.Valor).HasColumnType("numeric(18,2)");
        b.Property(x => x.Motivo).HasMaxLength(120).IsRequired();

        // Uma competição paga cada time uma vez só.
        b.HasIndex(x => new { x.LigaId, x.TimeId }).IsUnique();

        b.HasOne(x => x.Liga).WithMany().HasForeignKey(x => x.LigaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PedidoDemissaoConfiguration : IEntityTypeConfiguration<PedidoDemissao>
{
    public void Configure(EntityTypeBuilder<PedidoDemissao> b)
    {
        b.ToTable("PedidosDemissao");
        b.HasKey(x => x.PedidoId);

        // Um pedido por ultimato fracassado.
        b.HasIndex(x => new { x.TimeId, x.PartidaFalhaId }).IsUnique();
        b.HasIndex(x => x.Temporada);

        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Treinador).WithMany().HasForeignKey(x => x.TreinadorId).OnDelete(DeleteBehavior.SetNull);
    }
}
