using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class NegotiationPlayerConfiguration : IEntityTypeConfiguration<NegotiationPlayer>
{
    public void Configure(EntityTypeBuilder<NegotiationPlayer> builder)
    {
        builder.HasKey(x => x.NegotiationPlayerId);

        builder.Property(x => x.Papel)
            .IsRequired()
            .HasMaxLength(16);

        builder.HasIndex(x => x.NegotiationId)
            .HasDatabaseName("IX_NegotiationPlayer_NegotiationId");

        builder.HasOne(x => x.Player)
            .WithMany()
            .HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Negotiation)
            .WithMany(n => n.Players)
            .HasForeignKey(x => x.NegotiationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t =>
            t.HasCheckConstraint("CK_NegotiationPlayer_Papel", "[Papel] IN ('OFFERED','REQUESTED')"));
    }
}
