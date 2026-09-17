using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class DraftProtecaoConfiguration : IEntityTypeConfiguration<DraftProtecao>
{
    public void Configure(EntityTypeBuilder<DraftProtecao> e)
    {
        e.HasKey(x => new { x.DraftId, x.PlayerId });
        e.HasIndex(x => new { x.DraftId, x.TeamId });

        e.HasOne(x => x.Draft)
         .WithMany(d => d.Protecoes)
         .HasForeignKey(x => x.DraftId)
         .OnDelete(DeleteBehavior.Cascade);

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
