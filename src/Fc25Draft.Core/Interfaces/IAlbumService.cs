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

    /// <summary>Figurinhas de Lenda do álbum e os aposentados que ainda podem entrar.</summary>
    Task<LendasDoAlbumDto> GetLendasAsync(Guid albumId, CancellationToken ct);

    /// <summary>
    /// Coloca um aposentado como figurinha de Lenda (lendária) na página do último clube, com o próximo número
    /// do álbum. No máximo <c>AlbumFigurinhas.MaximoLendasPorAlbum</c> por álbum; cada jogador vira lenda uma vez.
    /// </summary>
    Task ColocarLendaAsync(Guid albumId, int playerId, string? destaque, string? adminToken, CancellationToken ct);

    /// <summary>Tira a figurinha de Lenda, só enquanto ninguém a tirou em pacote.</summary>
    Task TirarLendaAsync(Guid albumId, Guid figurinhaId, string? adminToken, CancellationToken ct);

    /// <summary>Jogadores do álbum que estão sem foto hoje.</summary>
    Task<IReadOnlyList<FigurinhaSemFotoDto>> SemFotoAsync(Guid albumId, CancellationToken ct);

    /// <summary>Dá pacotes a uma pessoa (testes, premiação). Fica no log de ações do admin.</summary>
    Task<int> DarPacotesAsync(Guid treinadorId, int quantidade, string? motivo, string? adminToken, CancellationToken ct);

    Task<IReadOnlyList<PacotesDoTreinadorDto>> PacotesPorTreinadorAsync(CancellationToken ct);

    /// <summary>Muda o X do bolão do álbum (1 pacote a cada X pontos). Fica no log de ações.</summary>
    Task SalvarPontosBolaoAsync(Guid albumId, int pontosPorPacote, string? adminToken, CancellationToken ct);

    /// <summary>
    /// Dá os pacotes que faltam pelos jogos e pelo bolão desde o lançamento do álbum ativo. Rodar de
    /// novo não duplica nada (a chave de cada pacote é única por pessoa).
    /// </summary>
    Task<ReconciliacaoPacotesDto> ReconciliarAsync(CancellationToken ct);

    /// <summary>
    /// Passa a contar também os jogos da temporada do álbum encerrados antes do lançamento (fica no log) e
    /// já dá os pacotes que faltam. Rodar de novo só reconcilia.
    /// </summary>
    Task<ReconciliacaoPacotesDto> ContarJogosAntesDoLancamentoAsync(string? adminToken, CancellationToken ct);

    // ---- A pessoa ----

    /// <summary>O álbum da pessoa (o ativo, se <paramref name="albumId"/> vier nulo).</summary>
    Task<MeuAlbumDto?> MeuAlbumAsync(Guid treinadorId, Guid? albumId, CancellationToken ct);

    /// <summary>Números do álbum ativo da pessoa (cartão da Minha Área); nulo sem álbum lançado.</summary>
    Task<AlbumResumoDoTreinadorDto?> ResumoAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>
    /// Dá os pacotes do dia (data de Brasília). Falso se a pessoa já pegou os de hoje.
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

    // ---- Trocas ----

    /// <summary>Repetidas, o que falta, propostas (recebidas, enviadas e histórico) e sugestões da pessoa.</summary>
    Task<TrocasDoTreinadorDto?> TrocasAsync(Guid treinadorId, CancellationToken ct);

    /// <summary>As repetidas dos dois para montar uma proposta com <paramref name="outroId"/>.</summary>
    Task<MontarTrocaDto?> MontarTrocaAsync(Guid treinadorId, Guid outroId, CancellationToken ct);

    /// <summary>
    /// Propõe uma troca: <paramref name="oferecidas"/> são repetidas de quem propõe; <paramref name="pedidas"/>,
    /// repetidas do outro. Com <paramref name="contrapropostaDe"/>, responde uma proposta recebida.
    /// </summary>
    Task<TrocaDto> ProporTrocaAsync(Guid deId, Guid paraId, IReadOnlyList<Guid> oferecidas, IReadOnlyList<Guid> pedidas,
        Guid? contrapropostaDe, CancellationToken ct);

    /// <summary>
    /// Aceita a proposta (só quem recebeu). Trava os dois, confere de novo as repetidas e move tudo numa
    /// transação só; se alguém não tiver mais, a proposta vira "não vale mais" e a mensagem diz por quê.
    /// </summary>
    Task<ResultadoDaTrocaDto> AceitarTrocaAsync(Guid trocaId, Guid treinadorId, CancellationToken ct);

    Task RecusarTrocaAsync(Guid trocaId, Guid treinadorId, CancellationToken ct);

    /// <summary>Quem propôs desiste (só enquanto está pendente).</summary>
    Task CancelarTrocaAsync(Guid trocaId, Guid treinadorId, CancellationToken ct);

    /// <summary>Marca como expiradas as propostas que passaram do prazo. Retorna quantas.</summary>
    Task<int> ExpirarTrocasAsync(CancellationToken ct);

    /// <summary>
    /// Troca <see cref="Core.Utilities.AlbumFigurinhas.RepetidasPorPacote"/> repetidas escolhidas (pode repetir a mesma
    /// figurinha se tiver cópias) por 1 pacote novo.
    /// </summary>
    Task ReciclarAsync(Guid treinadorId, IReadOnlyList<Guid> figurinhas, CancellationToken ct);

    /// <summary>Tira a marca de nova das figurinhas que a pessoa já viu na página.</summary>
    Task MarcarVistasAsync(Guid treinadorId, IReadOnlyCollection<Guid> figurinhas, CancellationToken ct);
}
