using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Álbum de figurinhas: lançamento pelo admin, pacotes e o álbum de cada pessoa.</summary>
public interface IAlbumService
{
    /// <summary>Todos os álbuns, do mais novo para o mais antigo.</summary>
    Task<IReadOnlyList<AlbumDto>> ListarAsync(CancellationToken ct);

    /// <summary>O álbum que está valendo (onde os pacotes abrem); nulo se nenhum foi lançado.</summary>
    Task<AlbumDto?> AtivoAsync(CancellationToken ct);

    // ---- Admin ----

    /// <summary>
    /// O que o álbum da temporada teria se fosse lançado agora: clubes, figurinhas por raridade, quantas
    /// sem foto e as sugestões de lendárias (maiores médias de nota da temporada anterior).
    /// </summary>
    Task<AlbumPreviaDto> PreviaAsync(int temporada, CancellationToken ct);

    /// <summary>
    /// Lança o álbum da temporada a partir dos elencos de hoje e o deixa ativo. Recusa se a temporada já
    /// tiver álbum.
    /// </summary>
    Task<AlbumDto> LancarAsync(int temporada, string? nome, IReadOnlyList<LendariaEscolhaDto> lendarias, string? adminToken, CancellationToken ct);

    Task<AlbumAdminDto?> GetAdminAsync(Guid albumId, CancellationToken ct);

    /// <summary>Troca as lendárias. Só enquanto nenhum pacote do álbum foi aberto.</summary>
    Task SalvarLendariasAsync(Guid albumId, IReadOnlyList<LendariaEscolhaDto> lendarias, string? adminToken, CancellationToken ct);

    /// <summary>Jogadores do álbum que estão sem foto hoje.</summary>
    Task<IReadOnlyList<FigurinhaSemFotoDto>> SemFotoAsync(Guid albumId, CancellationToken ct);

    /// <summary>Dá pacotes a uma pessoa (testes, premiação). Fica no log de ações do admin.</summary>
    Task<int> DarPacotesAsync(Guid treinadorId, int quantidade, string? motivo, string? adminToken, CancellationToken ct);

    Task<IReadOnlyList<PacotesDoTreinadorDto>> PacotesPorTreinadorAsync(CancellationToken ct);

    // ---- A pessoa ----

    /// <summary>O álbum da pessoa (o ativo, se <paramref name="albumId"/> vier nulo).</summary>
    Task<MeuAlbumDto?> MeuAlbumAsync(Guid treinadorId, Guid? albumId, CancellationToken ct);

    /// <summary>Abre o pacote mais antigo da pessoa: sorteia no servidor e cola as novas.</summary>
    Task<PacoteAbertoDto> AbrirPacoteAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>Tira a marca de nova das figurinhas que a pessoa já viu na página.</summary>
    Task MarcarVistasAsync(Guid treinadorId, IReadOnlyCollection<Guid> figurinhas, CancellationToken ct);
}
