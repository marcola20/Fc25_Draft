using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fc25Draft.Infra.Configurations;

public class AlbumConfiguration : IEntityTypeConfiguration<Album>
{
    public void Configure(EntityTypeBuilder<Album> b)
    {
        b.ToTable("Albuns");
        b.HasKey(x => x.AlbumId);

        b.Property(x => x.Nome).HasMaxLength(80).IsRequired();
        b.Property(x => x.PontosBolaoPorPacote).HasDefaultValue(AlbumFigurinhas.PontosBolaoPorPacotePadrao);

        // Um álbum por temporada.
        b.HasIndex(x => x.Temporada).IsUnique();

        b.HasMany(x => x.Figurinhas)
            .WithOne(x => x.Album)
            .HasForeignKey(x => x.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class FigurinhaConfiguration : IEntityTypeConfiguration<Figurinha>
{
    public void Configure(EntityTypeBuilder<Figurinha> b)
    {
        b.ToTable("Figurinhas");
        b.HasKey(x => x.FigurinhaId);

        b.Property(x => x.NomeImpresso).HasMaxLength(120).IsRequired();
        b.Property(x => x.PosicaoSigla).HasMaxLength(5);
        b.Property(x => x.Destaque).HasMaxLength(80);

        b.HasIndex(x => new { x.AlbumId, x.Numero }).IsUnique();
        b.HasIndex(x => new { x.AlbumId, x.Raridade });

        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Jogador).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
        // Pessoa excluída: a figurinha fica com o nome do lançamento, sem o perfil.
        b.HasOne(x => x.Treinador).WithMany().HasForeignKey(x => x.TreinadorId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class FigurinhaDoTreinadorConfiguration : IEntityTypeConfiguration<FigurinhaDoTreinador>
{
    public void Configure(EntityTypeBuilder<FigurinhaDoTreinador> b)
    {
        b.ToTable("FigurinhasDosTreinadores");
        b.HasKey(x => new { x.TreinadorId, x.FigurinhaId });

        b.HasIndex(x => x.FigurinhaId);

        b.HasOne(x => x.Treinador).WithMany().HasForeignKey(x => x.TreinadorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Figurinha).WithMany().HasForeignKey(x => x.FigurinhaId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PacoteGanhoConfiguration : IEntityTypeConfiguration<PacoteGanho>
{
    public void Configure(EntityTypeBuilder<PacoteGanho> b)
    {
        b.ToTable("PacotesGanhos");
        b.HasKey(x => x.PacoteId);

        b.Property(x => x.Origem).HasMaxLength(20).IsRequired();
        b.Property(x => x.Chave).HasMaxLength(80).IsRequired();
        b.Property(x => x.Motivo).HasMaxLength(200);

        // Reconciliar de novo nunca dá pacote em dobro.
        b.HasIndex(x => new { x.TreinadorId, x.Chave }).IsUnique();
        b.HasIndex(x => new { x.TreinadorId, x.AbertoEm });
        b.HasIndex(x => x.Origem);

        b.HasOne(x => x.Treinador).WithMany().HasForeignKey(x => x.TreinadorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Album).WithMany().HasForeignKey(x => x.AlbumId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class AlbumConquistaConfiguration : IEntityTypeConfiguration<AlbumConquista>
{
    public void Configure(EntityTypeBuilder<AlbumConquista> b)
    {
        b.ToTable("AlbumConquistas");
        b.HasKey(x => x.ConquistaId);

        // Um selo de cada por pessoa: a página de cada clube e o álbum (TeamId nulo) uma vez só.
        b.HasIndex(x => new { x.TreinadorId, x.AlbumId, x.Tipo, x.TeamId }).IsUnique().AreNullsDistinct(false);
        b.HasIndex(x => new { x.AlbumId, x.Tipo, x.Em });

        b.HasOne(x => x.Treinador).WithMany().HasForeignKey(x => x.TreinadorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Album).WithMany().HasForeignKey(x => x.AlbumId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Time).WithMany().HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TrocaFigurinhasConfiguration : IEntityTypeConfiguration<TrocaFigurinhas>
{
    public void Configure(EntityTypeBuilder<TrocaFigurinhas> b)
    {
        b.ToTable("TrocasFigurinhas");
        b.HasKey(x => x.TrocaId);

        b.Property(x => x.Motivo).HasMaxLength(300);

        b.HasIndex(x => new { x.ParaTreinadorId, x.Status });
        b.HasIndex(x => new { x.DeTreinadorId, x.Status });
        b.HasIndex(x => new { x.Status, x.ExpiraEm });

        b.HasOne(x => x.Album).WithMany().HasForeignKey(x => x.AlbumId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.De).WithMany().HasForeignKey(x => x.DeTreinadorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Para).WithMany().HasForeignKey(x => x.ParaTreinadorId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<TrocaFigurinhas>().WithMany().HasForeignKey(x => x.ContrapropostaDeId).OnDelete(DeleteBehavior.SetNull);

        b.HasMany(x => x.Itens).WithOne(x => x.Troca).HasForeignKey(x => x.TrocaId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class TrocaFigurinhaItemConfiguration : IEntityTypeConfiguration<TrocaFigurinhaItem>
{
    public void Configure(EntityTypeBuilder<TrocaFigurinhaItem> b)
    {
        b.ToTable("TrocasFigurinhasItens");
        // Uma cópia de cada figurinha por lado.
        b.HasKey(x => new { x.TrocaId, x.FigurinhaId, x.Oferecida });

        b.HasOne(x => x.Figurinha).WithMany().HasForeignKey(x => x.FigurinhaId).OnDelete(DeleteBehavior.Cascade);
    }
}
