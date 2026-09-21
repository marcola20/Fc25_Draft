using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class PremiacaoConfiguration : IEntityTypeConfiguration<Premiacao>
{
    public void Configure(EntityTypeBuilder<Premiacao> b)
    {
        b.ToTable("Premiacoes");
        b.HasKey(x => x.PremiacaoId);

        b.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        b.HasIndex(x => x.Temporada).IsUnique();

        b.HasMany(x => x.Itens)
            .WithOne(x => x.Premiacao)
            .HasForeignKey(x => x.PremiacaoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PremiacaoItemConfiguration : IEntityTypeConfiguration<PremiacaoItem>
{
    public void Configure(EntityTypeBuilder<PremiacaoItem> b)
    {
        b.ToTable("PremiacaoItens");
        b.HasKey(x => x.PremiacaoItemId);

        b.Property(x => x.Valor).HasColumnType("numeric(18,2)");
        b.HasIndex(x => new { x.PremiacaoId, x.Tipo, x.Divisao });
    }
}

public class PremiacaoPagamentoConfiguration : IEntityTypeConfiguration<PremiacaoPagamento>
{
    public void Configure(EntityTypeBuilder<PremiacaoPagamento> b)
    {
        b.ToTable("PremiacaoPagamentos");
        b.HasKey(x => x.PagamentoId);

        b.Property(x => x.Valor).HasColumnType("numeric(18,2)");
        b.Property(x => x.Motivo).HasMaxLength(120).IsRequired();

        // Uma competição paga cada time uma vez só.
        b.HasIndex(x => new { x.LigaId, x.TimeId }).IsUnique();

        b.HasOne(x => x.Premiacao).WithMany().HasForeignKey(x => x.PremiacaoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Liga).WithMany().HasForeignKey(x => x.LigaId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.Cascade);
    }
}
