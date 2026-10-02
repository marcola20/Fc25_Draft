using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.DTOs;

/// <summary>Situação de um time no fim da temporada e onde ele joga na temporada seguinte.</summary>
public record TemporadaTimeDto(
    Guid TimeId,
    string Nome,
    Divisao Divisao,
    int Posicao,
    ZonaClassificacao Zona,
    Divisao? DivisaoProxima,
    string Situacao);

public record TemporadaPlayoffDto(
    Guid PartidaId,
    Guid TimeSerieAId,
    string TimeSerieANome,
    int? GolsSerieA,
    Guid TimeSerieBId,
    string TimeSerieBNome,
    int? GolsSerieB,
    PartidaStatus Status,
    Guid? VencedorId,
    string? VencedorNome);

/// <summary>Time sem divisão na temporada (ex.: entrou pelo draft de expansão).</summary>
public record TemporadaTimeLivreDto(Guid TimeId, string Nome, int Elenco);

/// <summary>Um jogo da Supercopa (semifinal ou final), para a tela da temporada.</summary>
public record TemporadaSupercopaJogoDto(
    string Fase,
    string CasaNome,
    string ForaNome,
    int GolsCasa,
    int GolsFora,
    PartidaStatus Status,
    string? VencedorNome);

/// <summary>
/// Supercopa da temporada: campeão da Série A x campeão da Copa, em jogo único. Quando o mesmo time
/// ganha as duas, há semifinal entre o vice da Série A e o vice da Copa, e o vencedor pega o campeão na final.
/// </summary>
public record TemporadaSupercopaDto(
    Guid? LigaId,
    string? Nome,
    LigaStatus? Status,
    Guid? CampeaoSerieAId,
    string? CampeaoSerieANome,
    Guid? CampeaoCopaId,
    string? CampeaoCopaNome,
    int? GolsSerieA,
    int? GolsCopa,
    Guid? CampeaoId,
    string? CampeaoNome,
    bool PodeCriar,
    string? Impedimento,
    bool ComSemifinal = false,
    string? ViceSerieANome = null,
    string? ViceCopaNome = null,
    IReadOnlyList<TemporadaSupercopaJogoDto>? Jogos = null,
    bool PodeCriarFinal = false);

public record TemporadaResumoDto(
    int Temporada,
    LigaDto? SerieA,
    LigaDto? SerieB,
    IReadOnlyList<TemporadaTimeDto> Times,
    IReadOnlyList<TemporadaPlayoffDto> Playoffs,
    IReadOnlyList<TemporadaTimeLivreDto> TimesSemDivisao,
    bool PodeCriarPlayoff,
    bool PodeGerarProxima,
    string? Impedimento,
    bool ProximaTemporadaJaExiste,
    TemporadaSupercopaDto Supercopa);

public record GerarProximaTemporadaRequest(
    int TemporadaOrigem,
    string NomeSerieA,
    string? NomeSerieB,
    DateTime DataInicio,
    DateTime DataFim,
    int VagasDiretasSerieA = 1,
    int VagasPlayoffSerieA = 1,
    int VagasDiretasSerieB = 1,
    int VagasPlayoffSerieB = 1,
    IReadOnlyList<Guid>? TimesExtrasSerieB = null,
    bool CriarSerieB = true,
    bool ZerarContadores = true,
    bool CancelarPropostas = true,
    bool LimparListaTransferencias = true,
    bool RemoverMinimosTemporarios = false);

/// <summary>O que a virada de temporada mexe no mercado, para a tela mostrar antes de gerar.</summary>
/// <param name="TimesComContador">Times com transferências, empréstimos ou vendas rápidas usados na janela.</param>
/// <param name="MinimosTemporarios">Times com mínimo de elenco temporário, já com o valor ("Time (12)").</param>
public record TemporadaViradaMercadoDto(
    int Emprestimos,
    int PropostasPendentes,
    int JogadoresNaLista,
    int TimesComContador,
    IReadOnlyList<string> MinimosTemporarios);
