using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class EmprestimoConfiguration : IEntityTypeConfiguration<Emprestimo>
{
    public void Configure(EntityTypeBuilder<Emprestimo> e)
    {
        e.ToTable("Emprestimos");
        e.HasKey(x => x.EmprestimoId);

        e.Property(x => x.Status).HasConversion<int>().IsRequired();
        e.Property(x => x.ValorOpcaoCompra).HasColumnType("numeric(18,2)");

        // Um jogador só pode estar em um empréstimo em andamento por vez.
        e.HasIndex(x => x.PlayerId)
         .IsUnique()
         .HasFilter($"\"Status\" = {(int)EmprestimoStatus.Ativo}");
        e.HasIndex(x => x.DonoTeamId);
        e.HasIndex(x => x.TomadorTeamId);

        e.HasOne(x => x.Player)
         .WithMany()
         .HasForeignKey(x => x.PlayerId)
         .OnDelete(DeleteBehavior.Restrict);

        e.HasOne(x => x.DonoTeam)
         .WithMany()
         .HasForeignKey(x => x.DonoTeamId)
         .OnDelete(DeleteBehavior.NoAction);

        e.HasOne(x => x.TomadorTeam)
         .WithMany()
         .HasForeignKey(x => x.TomadorTeamId)
         .OnDelete(DeleteBehavior.NoAction);
    }
}
