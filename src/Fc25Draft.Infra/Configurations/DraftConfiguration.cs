using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class DraftConfiguration : IEntityTypeConfiguration<Draft>
{
    public void Configure(EntityTypeBuilder<Draft> e)
    {
        e.HasKey(x => x.DraftId);
        e.Property(x => x.Tipo).HasConversion<int>().HasDefaultValue(DraftTipo.Normal);
        e.Property(x => x.FatorCompensacao).HasColumnType("numeric(6,3)");
    }
}
