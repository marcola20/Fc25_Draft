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
    bool ProximaTemporadaJaExiste);

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
    bool CriarSerieB = true);
