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

    /// <summary>Muda o X do bolão do álbum (1 pacote a cada X pontos). Fica no log de ações.</summary>
    Task SalvarPontosBolaoAsync(Guid albumId, int pontosPorPacote, string? adminToken, CancellationToken ct);

    /// <summary>
    /// Dá os pacotes que faltam pelas vitórias e pelo bolão desde o lançamento do álbum ativo. Rodar de
    /// novo não duplica nada (a chave de cada pacote é única por pessoa).
    /// </summary>
    Task<ReconciliacaoPacotesDto> ReconciliarAsync(CancellationToken ct);

    // ---- A pessoa ----

    /// <summary>O álbum da pessoa (o ativo, se <paramref name="albumId"/> vier nulo).</summary>
    Task<MeuAlbumDto?> MeuAlbumAsync(Guid treinadorId, Guid? albumId, CancellationToken ct);

    /// <summary>Números do álbum ativo da pessoa (cartão da Minha Área); nulo sem álbum lançado.</summary>
    Task<AlbumResumoDoTreinadorDto?> ResumoAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>
    /// Dá o pacote do dia (data de Brasília). Falso se a pessoa já pegou o de hoje.
    /// </summary>
    Task<bool> PegarPacoteDoDiaAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>Abre o pacote mais antigo da pessoa: sorteia no servidor e cola as novas.</summary>
    Task<PacoteAbertoDto> AbrirPacoteAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>As últimas raras tiradas no álbum ativo (brilhantes de jogador e lendárias), da mais nova.</summary>
    Task<IReadOnlyList<RaraTiradaDto>> UltimasRarasAsync(int quantas, CancellationToken ct);

    /// <summary>Ranking de colecionadores do álbum ativo; nulo sem álbum lançado.</summary>
    Task<ColecionadoresDto?> ColecionadoresAsync(CancellationToken ct);

    /// <summary>Quem completou cada álbum, para o Hall da Fama (Colecionadores).</summary>
    Task<IReadOnlyList<AlbumCompletoDto>> AlbunsCompletosAsync(CancellationToken ct);

    /// <summary>Tira a marca de nova das figurinhas que a pessoa já viu na página.</summary>
    Task MarcarVistasAsync(Guid treinadorId, IReadOnlyCollection<Guid> figurinhas, CancellationToken ct);
}
