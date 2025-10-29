using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Data.Configurations;

public class BudgetLedgerConfiguration : IEntityTypeConfiguration<BudgetLedger>
{
    public void Configure(EntityTypeBuilder<BudgetLedger> builder)
    {
        builder.HasKey(x => x.BudgetLedgerId);

        builder.Property(x => x.DataUtc)
            .IsRequired();

        builder.Property(x => x.Tipo)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.Origem)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Valor)
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(x => x.Descricao)
            .HasMaxLength(256);

        builder.HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TeamId, x.DataUtc })
            .HasDatabaseName("IX_BudgetLedger_TeamId_DataUtc")
            .IsDescending(false, true);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("CK_BudgetLedger_Tipo", '"Tipo" IN (''CREDIT'',''DEBIT'')');
            t.HasCheckConstraint("CK_BudgetLedger_Valor", '"Valor" > 0');
        });
    }
}
