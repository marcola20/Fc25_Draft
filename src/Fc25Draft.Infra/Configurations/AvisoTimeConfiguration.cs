using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class AvisoTimeConfiguration : IEntityTypeConfiguration<AvisoTime>
{
    public void Configure(EntityTypeBuilder<AvisoTime> b)
    {
        b.ToTable("AvisosTimes");
        b.HasKey(x => x.AvisoId);
        b.Property(x => x.Tipo).HasMaxLength(40).IsRequired();
        b.Property(x => x.Texto).HasMaxLength(400).IsRequired();
        b.Property(x => x.Link).HasMaxLength(200);
        b.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.TeamId, x.CriadoEm });
        // Fila das notificações no celular: só os ainda não enviados.
        b.HasIndex(x => x.PushEnviadoEm).HasFilter("\"PushEnviadoEm\" IS NULL");
    }
}
