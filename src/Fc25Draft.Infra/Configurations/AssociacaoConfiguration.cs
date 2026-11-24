using Fc25Draft.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class EleicaoAssociacaoConfiguration : IEntityTypeConfiguration<EleicaoAssociacao>
{
    public void Configure(EntityTypeBuilder<EleicaoAssociacao> b)
    {
        b.ToTable("AssociacaoEleicoes");
        b.HasKey(x => x.EleicaoId);
        b.Property(x => x.Titulo).HasMaxLength(160).IsRequired();
        b.Property(x => x.Edital).IsRequired().HasDefaultValue("");
        b.Property(x => x.Comissao).HasMaxLength(500).IsRequired().HasDefaultValue("");
        b.Property(x => x.DecisaoPresidencia).HasMaxLength(500);
        b.HasIndex(x => x.CriadaEm);
    }
}

public class ChapaAssociacaoConfiguration : IEntityTypeConfiguration<ChapaAssociacao>
{
    public void Configure(EntityTypeBuilder<ChapaAssociacao> b)
    {
        b.ToTable("AssociacaoChapas");
        b.HasKey(x => x.ChapaId);
        b.Property(x => x.Nome).HasMaxLength(120).IsRequired();
        b.Property(x => x.Plano).IsRequired().HasDefaultValue("");
        b.HasIndex(x => new { x.EleicaoId, x.Numero }).IsUnique();

        b.HasOne(x => x.Eleicao).WithMany(e => e.Chapas).HasForeignKey(x => x.EleicaoId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ChapaMembroConfiguration : IEntityTypeConfiguration<ChapaMembro>
{
    public void Configure(EntityTypeBuilder<ChapaMembro> b)
    {
        b.ToTable("AssociacaoChapaMembros");
        b.HasKey(x => new { x.ChapaId, x.Cargo });
        b.HasIndex(x => x.TreinadorId);

        b.HasOne(x => x.Chapa).WithMany(c => c.Membros).HasForeignKey(x => x.ChapaId).OnDelete(DeleteBehavior.Cascade);
        // Quem já foi de chapa fica no histórico: não some ao apagar o cadastro.
        b.HasOne(x => x.Treinador).WithMany().HasForeignKey(x => x.TreinadorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EleicaoEleitorConfiguration : IEntityTypeConfiguration<EleicaoEleitor>
{
    public void Configure(EntityTypeBuilder<EleicaoEleitor> b)
    {
        b.ToTable("AssociacaoEleitores");
        b.HasKey(x => new { x.EleicaoId, x.TreinadorId });

        b.HasOne(x => x.Eleicao).WithMany(e => e.Eleitores).HasForeignKey(x => x.EleicaoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Treinador).WithMany().HasForeignKey(x => x.TreinadorId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class EleicaoVotoConfiguration : IEntityTypeConfiguration<EleicaoVoto>
{
    public void Configure(EntityTypeBuilder<EleicaoVoto> b)
    {
        // Sem eleitor e sem horário, de propósito (voto secreto).
        b.ToTable("AssociacaoVotos");
        b.HasKey(x => x.VotoId);
        b.HasIndex(x => x.EleicaoId);

        b.HasOne<EleicaoAssociacao>().WithMany().HasForeignKey(x => x.EleicaoId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ChapaAssociacao>().WithMany().HasForeignKey(x => x.ChapaId).OnDelete(DeleteBehavior.Cascade);
    }
}
