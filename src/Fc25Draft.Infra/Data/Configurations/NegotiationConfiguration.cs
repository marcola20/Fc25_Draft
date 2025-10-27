using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class NegotiationConfiguration : IEntityTypeConfiguration<Negotiation>
{
    public void Configure(EntityTypeBuilder<Negotiation> builder)
    {
        builder.HasKey(x => x.NegotiationId);

        builder.Property(x => x.ValorOferecido)
            .HasColumnType("decimal(18,2)");

        builder.Property(x => x.DataInicioUtc)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.Tipo)
            .IsRequired()
            .HasMaxLength(16);

        builder.Property(x => x.Observacao)
            .HasMaxLength(512);

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_Negotiation_Status");

        builder.HasOne(x => x.OrigemTeam)
            .WithMany()
            .HasForeignKey(x => x.OrigemTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DestinoTeam)
            .WithMany()
            .HasForeignKey(x => x.DestinoTeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Players)
            .WithOne(p => p.Negotiation)
            .HasForeignKey(p => p.NegotiationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "CK_Negotiation_Status",
                "[Status] IN ('PENDING','ACCEPTED','REJECTED','CANCELLED','COMPLETED')");
            t.HasCheckConstraint(
                "CK_Negotiation_Tipo",
                "[Tipo] IN ('TRADE','SALE')");
        });
    }
}
