using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class VotacaoPremiosConfiguration : IEntityTypeConfiguration<VotacaoPremios>
{
    public void Configure(EntityTypeBuilder<VotacaoPremios> b)
    {
        b.ToTable("VotacoesPremios");
        b.HasKey(x => x.Temporada);
        b.Property(x => x.Temporada).ValueGeneratedNever();
    }
}

public class PremioCandidatoConfiguration : IEntityTypeConfiguration<PremioCandidato>
{
    public void Configure(EntityTypeBuilder<PremioCandidato> b)
    {
        b.ToTable("PremiosCandidatos");
        b.HasKey(x => x.CandidatoId);
        b.Property(x => x.Resumo).HasMaxLength(200);
        b.HasIndex(x => new { x.Temporada, x.Categoria });

        b.HasOne(x => x.Votacao)
            .WithMany(v => v.Candidatos)
            .HasForeignKey(x => x.Temporada)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Treinador).WithMany().HasForeignKey(x => x.TreinadorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TimeId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne(x => x.OverPara).WithMany().HasForeignKey(x => x.OverParaPlayerId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class PremioVotoConfiguration : IEntityTypeConfiguration<PremioVoto>
{
    public void Configure(EntityTypeBuilder<PremioVoto> b)
    {
        b.ToTable("PremiosVotos");
        b.HasKey(x => x.VotoId);

        // Cada pessoa marca um candidato uma vez só.
        b.HasIndex(x => new { x.CandidatoId, x.EleitorId }).IsUnique();
        b.HasIndex(x => x.EleitorId);

        b.HasOne(x => x.Candidato).WithMany().HasForeignKey(x => x.CandidatoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Eleitor).WithMany().HasForeignKey(x => x.EleitorId).OnDelete(DeleteBehavior.Cascade);
    }
}
