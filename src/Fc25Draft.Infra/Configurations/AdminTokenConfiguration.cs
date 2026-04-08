using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class AdminTokenConfiguration : IEntityTypeConfiguration<AdminToken>
{
    public void Configure(EntityTypeBuilder<AdminToken> e)
    {
        e.ToTable("Token_Administrador");
        e.HasKey(x => x.AdminTokenId);

        e.Property(x => x.AdminTokenId)
         .ValueGeneratedOnAdd();

        e.Property(x => x.Token)
         .IsRequired();

        e.HasIndex(x => x.Token)
         .IsUnique();
    }
}
