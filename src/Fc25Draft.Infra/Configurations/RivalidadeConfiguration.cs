using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class RivalidadeConfiguration : IEntityTypeConfiguration<Rivalidade>
{
    public void Configure(EntityTypeBuilder<Rivalidade> b)
    {
        b.ToTable("Rivalidades");
        b.HasKey(x => x.RivalidadeId);

        b.Property(x => x.Nome).HasMaxLength(80);
        b.HasIndex(x => new { x.TreinadorAId, x.TreinadorBId }).IsUnique();

        b.HasOne(x => x.TreinadorA).WithMany().HasForeignKey(x => x.TreinadorAId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.TreinadorB).WithMany().HasForeignKey(x => x.TreinadorBId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ClassicoPremioConfiguration : IEntityTypeConfiguration<ClassicoPremio>
{
    public void Configure(EntityTypeBuilder<ClassicoPremio> b)
    {
        b.ToTable("ClassicoPremios");
        b.HasKey(x => x.PremioId);

        // Um prêmio por jogo: a apuração roda de minuto em minuto e não pode pagar duas vezes.
        b.HasIndex(x => x.PartidaId).IsUnique();
        b.Property(x => x.Valor).HasColumnType("numeric(18,2)");

        b.HasOne(x => x.Partida).WithMany().HasForeignKey(x => x.PartidaId).OnDelete(DeleteBehavior.Cascade);

        // O prêmio pago fica no histórico mesmo se a rivalidade for desfeita ou a pessoa/clube sair da liga.
        b.HasOne(x => x.Rivalidade).WithMany().HasForeignKey(x => x.RivalidadeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Treinador).WithMany().HasForeignKey(x => x.TreinadorId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.SetNull);
    }
}
