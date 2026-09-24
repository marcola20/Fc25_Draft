using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class TeamObservacaoConfiguration : IEntityTypeConfiguration<TeamObservacao>
{
    public void Configure(EntityTypeBuilder<TeamObservacao> e)
    {
        e.ToTable("TeamObservacoes");
        e.HasKey(x => new { x.TeamId, x.PlayerId });

        e.HasOne(x => x.Team)
         .WithMany()
         .HasForeignKey(x => x.TeamId)
         .OnDelete(DeleteBehavior.Cascade);

        e.HasOne(x => x.Player)
         .WithMany()
         .HasForeignKey(x => x.PlayerId)
         .OnDelete(DeleteBehavior.Cascade);
    }
}
