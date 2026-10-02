namespace Fc25Draft.Core.Interfaces;

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

/// <summary>Os números que o PES manda em cada partida importada (posse, chutes, passes…).</summary>
public interface IEstatisticasPesService
{
    /// <summary>Números da partida, na ordem de exibição; vazio quando ela não veio do PES.</summary>
    Task<IReadOnlyList<NumeroDaPartidaDto>> DaPartidaAsync(Guid partidaId, CancellationToken ct);

    /// <summary>Médias de cada time na competição, das partidas encerradas com números do PES.</summary>
    Task<IReadOnlyList<NumerosDoTimeDto>> DosTimesAsync(Guid ligaId, CancellationToken ct);
}
