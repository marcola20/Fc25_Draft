using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class EvolucaoVendaRapidaConfiguration : IEntityTypeConfiguration<EvolucaoVendaRapida>
{
    public void Configure(EntityTypeBuilder<EvolucaoVendaRapida> e)
    {
        e.ToTable("EvolucoesVendaRapida");
        e.HasKey(x => x.Overall);
        e.Property(x => x.Overall).ValueGeneratedNever();
    }
}
