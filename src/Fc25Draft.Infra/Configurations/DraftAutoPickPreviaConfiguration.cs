using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class DraftPlanejadoRodadaConfiguration : IEntityTypeConfiguration<DraftPlanejadoRodada>
{
    public void Configure(EntityTypeBuilder<DraftPlanejadoRodada> e)
    {
        e.ToTable("DraftPlanejadoRodadas");
        e.HasKey(x => x.RoundNumber);
        e.Property(x => x.RoundNumber).ValueGeneratedNever();
    }
}

public class DraftAutoPickPreviaConfiguration : IEntityTypeConfiguration<DraftAutoPickPrevia>
{
    public void Configure(EntityTypeBuilder<DraftAutoPickPrevia> e)
    {
        e.ToTable("DraftAutoPickPrevias");
        e.HasKey(x => x.TeamId);
        e.Property(x => x.Modo).HasConversion<int>();

        e.HasOne(x => x.Team)
         .WithMany()
         .HasForeignKey(x => x.TeamId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}

public class DraftAutoPickPreviaRodadaConfiguration : IEntityTypeConfiguration<DraftAutoPickPreviaRodada>
{
    public void Configure(EntityTypeBuilder<DraftAutoPickPreviaRodada> e)
    {
        e.ToTable("DraftAutoPickPreviaRodadas");
        e.HasKey(x => new { x.TeamId, x.RoundNumber });

        e.HasOne(x => x.Previa)
         .WithMany(p => p.Rodadas)
         .HasForeignKey(x => x.TeamId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Position)
         .WithMany()
         .HasForeignKey(x => x.PositionId)
         .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DraftAutoPickPreviaItemConfiguration : IEntityTypeConfiguration<DraftAutoPickPreviaItem>
{
    public void Configure(EntityTypeBuilder<DraftAutoPickPreviaItem> e)
    {
        e.ToTable("DraftAutoPickPreviaItens");
        e.HasKey(x => x.DraftAutoPickPreviaItemId);
        e.HasIndex(x => new { x.TeamId, x.PlayerId }).IsUnique();

        e.HasOne(x => x.Previa)
         .WithMany(p => p.Itens)
         .HasForeignKey(x => x.TeamId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Player)
         .WithMany()
         .HasForeignKey(x => x.PlayerId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
