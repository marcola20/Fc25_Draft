using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class InscricaoPushConfiguration : IEntityTypeConfiguration<InscricaoPush>
{
    public void Configure(EntityTypeBuilder<InscricaoPush> b)
    {
        b.ToTable("InscricoesPush");
        b.HasKey(x => x.InscricaoId);
        b.Property(x => x.Endpoint).HasMaxLength(1000).IsRequired();
        b.Property(x => x.P256dh).HasMaxLength(200).IsRequired();
        b.Property(x => x.Auth).HasMaxLength(100).IsRequired();
        b.Property(x => x.Aparelho).HasMaxLength(120);
        b.HasIndex(x => x.Endpoint).IsUnique();
        b.HasIndex(x => x.TreinadorId);
        b.HasOne(x => x.Treinador).WithMany().HasForeignKey(x => x.TreinadorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChavePushConfiguration : IEntityTypeConfiguration<ChavePush>
{
    public void Configure(EntityTypeBuilder<ChavePush> b)
    {
        b.ToTable("ChavesPush");
        b.HasKey(x => x.ChavePushId);
        b.Property(x => x.ChavePushId).ValueGeneratedNever();
        b.Property(x => x.PublicKey).HasMaxLength(200).IsRequired();
        b.Property(x => x.PrivateKey).HasMaxLength(100).IsRequired();
    }
}

public class NotificacaoEnviadaConfiguration : IEntityTypeConfiguration<NotificacaoEnviada>
{
    public void Configure(EntityTypeBuilder<NotificacaoEnviada> b)
    {
        b.ToTable("NotificacoesEnviadas");
        b.HasKey(x => x.Chave);
        b.Property(x => x.Chave).HasMaxLength(200);
    }
}
