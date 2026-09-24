using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class DraftAutoPickConfiguration : IEntityTypeConfiguration<DraftAutoPick>
{
    public void Configure(EntityTypeBuilder<DraftAutoPick> e)
    {
        e.ToTable("DraftAutoPicks");
        e.HasKey(x => new { x.DraftId, x.TeamId });
        e.Property(x => x.Modo).HasConversion<int>();

        e.HasOne(x => x.Draft)
         .WithMany()
         .HasForeignKey(x => x.DraftId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Team)
         .WithMany()
         .HasForeignKey(x => x.TeamId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DraftAutoPickRodadaConfiguration : IEntityTypeConfiguration<DraftAutoPickRodada>
{
    public void Configure(EntityTypeBuilder<DraftAutoPickRodada> e)
    {
        e.ToTable("DraftAutoPickRodadas");
        e.HasKey(x => new { x.DraftId, x.TeamId, x.RoundNumber });

        e.HasOne(x => x.Config)
         .WithMany(c => c.Rodadas)
         .HasForeignKey(x => new { x.DraftId, x.TeamId })
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Position)
         .WithMany()
         .HasForeignKey(x => x.PositionId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DraftAutoPickItemConfiguration : IEntityTypeConfiguration<DraftAutoPickItem>
{
    public void Configure(EntityTypeBuilder<DraftAutoPickItem> e)
    {
        e.ToTable("DraftAutoPickItens");
        e.HasKey(x => x.DraftAutoPickItemId);
        e.HasIndex(x => new { x.DraftId, x.TeamId, x.PlayerId }).IsUnique();
        e.HasIndex(x => new { x.DraftId, x.TeamId, x.PositionId, x.Ordem });

        e.HasOne(x => x.Config)
         .WithMany(c => c.Itens)
         .HasForeignKey(x => new { x.DraftId, x.TeamId })
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Player)
         .WithMany()
         .HasForeignKey(x => x.PlayerId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
