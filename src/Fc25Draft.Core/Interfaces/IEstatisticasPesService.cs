using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Um jogador na partida com a nota da liga (a oficial, calculada agora com os pesos em uso) e a decomposição dela.
/// <c>JogadorId</c> nulo = nome do PES que não casou com o cadastro; <c>Nome</c> é o do cadastro quando casou.
/// <c>MelhorEmCampo</c>: maior nota da liga do jogo.
/// </summary>
public record DesempenhoJogadorDto(
    int? JogadorId,
    string Nome,
    ResultadoNotaDaLiga NotaLiga,
    LigaEstatisticaJogador Numeros,
    bool MelhorEmCampo);

/// <summary>Os jogadores dos dois times com números do PES, na ordem do registro (titulares, depois quem entrou).</summary>
public record JogadoresDaPartidaDto(
    IReadOnlyList<DesempenhoJogadorDto> Casa,
    IReadOnlyList<DesempenhoJogadorDto> Fora);

/// <summary>
/// Registros do jogo somados na competição (jogos encerrados com o registro individual do PES), por jogador
/// ligado ao cadastro. Time e posição são os do último jogo. A nota da liga é a média das notas de cada jogo,
/// calculadas com os pesos em uso.
/// </summary>
public record RegistroDoJogadorDto(
    int JogadorId,
    string Nome,
    string? Posicao,
    Guid TimeId,
    string TimeNome,
    int Jogos,
    int Minutos,
    decimal NotaMedia,
    decimal MelhorNota,
    int Gols,
    int Assistencias,
    int Chutes,
    int ChutesAGol,
    int Passes,
    int PassesCertos,
    int Desarmes,
    int DesarmesCertos,
    int Interceptacoes,
    int Afastamentos,
    int Cruzamentos,
    int Toques,
    int Faltas,
    decimal DistanciaDeDrible,
    // Só de quem jogou de goleiro: nulo para os demais.
    int? Defesas,
    int? JogosNoGol);

/// <summary>Um número da partida para os dois times (posse, chutes…). <c>Percentual</c>: mostrar com "%".</summary>
public record NumeroDaPartidaDto(string Rotulo, decimal Casa, decimal Fora, bool Percentual = false);

/// <summary>Médias de um time na competição, só das partidas que têm os números do PES.</summary>
public record NumerosDoTimeDto(
    Guid TimeId,
    string TimeNome,
    int Jogos,
    decimal Posse,
    decimal ChutesPorJogo,
    decimal ChutesAGolPorJogo,
    // Chutes a gol sobre chutes, em %.
    decimal? Pontaria,
    // Passes certos sobre passes, em %.
    decimal? AcertoDePasses,
    decimal ChutesSofridosPorJogo,
    decimal ChutesAGolSofridosPorJogo,
    decimal DefesasPorJogo,
    decimal DesarmesPorJogo,
    decimal EscanteiosPorJogo,
    decimal FaltasPorJogo,
    // Gols feitos a cada chute a gol, em %.
    decimal? Conversao);

/// <summary>Um número da partida no formulário do admin; nulo nos dois lados = não informado.</summary>
public record CampoNumeroDto(string Chave, string Rotulo, decimal? Casa, decimal? Fora);

/// <summary>Os números que o PES manda em cada partida importada (posse, chutes, passes…).</summary>
public interface IEstatisticasPesService
{
    /// <summary>Números da partida, na ordem de exibição; vazio quando ela não veio do PES.</summary>
    Task<IReadOnlyList<NumeroDaPartidaDto>> DaPartidaAsync(Guid partidaId, CancellationToken ct);

    /// <summary>Os mesmos números, lidos de um JSON do PES (ex.: resultado ainda pendente).</summary>
    IReadOnlyList<NumeroDaPartidaDto> DoJson(string? json);

    /// <summary>
    /// Números de cada jogador e a nota da liga (<see cref="NotaDaLiga"/>), com o placar e os cartões da partida.
    /// Nulo quando a partida não tem os números por jogador.
    /// </summary>
    Task<JogadoresDaPartidaDto?> JogadoresDaPartidaAsync(Guid partidaId, CancellationToken ct);

    /// <summary>Registros do jogo de cada jogador somados na competição (sem o playoff de acesso); vazio quando não há.</summary>
    Task<IReadOnlyList<RegistroDoJogadorDto>> RegistrosDaCompeticaoAsync(Guid ligaId, CancellationToken ct);

    /// <summary>Os registros do jogador em cada partida que os tem (chave: partida), com a nota da liga.</summary>
    Task<IReadOnlyDictionary<Guid, DesempenhoJogadorDto>> RegistrosDoJogadorAsync(int playerId, CancellationToken ct);

    /// <summary>Médias de cada time na competição, das partidas encerradas com números do PES.</summary>
    Task<IReadOnlyList<NumerosDoTimeDto>> DosTimesAsync(Guid ligaId, CancellationToken ct);

    /// <summary>Todos os números editáveis da partida, preenchidos com o que já existe.</summary>
    Task<IReadOnlyList<CampoNumeroDto>> ParaEditarAsync(Guid partidaId, CancellationToken ct);

    /// <summary>
    /// Grava os números digitados pelo admin (partida sem JSON do PES, ou para corrigir). Mantém o resto do
    /// JSON da importação; campo vazio nos dois lados é apagado.
    /// </summary>
    Task SalvarAsync(Guid partidaId, IReadOnlyList<CampoNumeroDto> campos, CancellationToken ct);
}
