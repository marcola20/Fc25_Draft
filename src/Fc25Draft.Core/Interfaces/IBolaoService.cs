using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Bolão da rodada: cada pessoa da liga chuta os placares antes da bola rolar.</summary>
public interface IBolaoService
{
    /// <summary>As rodadas que ainda aceitam palpite, da mais próxima para a mais distante.</summary>
    Task<IReadOnlyList<BolaoRodadaDto>> RodadasAbertasAsync(Guid? treinadorId, CancellationToken ct);

    /// <summary>As rodadas que já fecharam, com os pontos de quem está olhando.</summary>
    Task<IReadOnlyList<BolaoRodadaDto>> RodadasFechadasAsync(Guid? treinadorId, int quantas, CancellationToken ct);

    Task<BolaoRodadaDto?> RodadaAsync(Guid rodadaId, Guid? treinadorId, CancellationToken ct);

    /// <summary>Grava os palpites. Recusa rodada que já começou.</summary>
    Task<BolaoRodadaDto> SalvarAsync(Guid treinadorId, Guid rodadaId, IReadOnlyList<BolaoPalpiteRequest> palpites, CancellationToken ct);

    /// <summary>O que cada um chutou num jogo. Só depois que a rodada fecha.</summary>
    Task<IReadOnlyList<BolaoPalpiteDeAlguemDto>> PalpitesDoJogoAsync(Guid partidaId, CancellationToken ct);

    /// <summary>Ranking geral ou de uma temporada.</summary>
    Task<IReadOnlyList<BolaoRankingLinhaDto>> RankingAsync(int? temporada, CancellationToken ct);

    /// <summary>Ranking de uma rodada só.</summary>
    Task<IReadOnlyList<BolaoRankingLinhaDto>> RankingDaRodadaAsync(Guid rodadaId, CancellationToken ct);

    /// <summary>As temporadas que já têm palpite, para o seletor do ranking.</summary>
    Task<IReadOnlyList<int>> TemporadasAsync(CancellationToken ct);

    Task<BolaoResumoDoTreinadorDto> ResumoAsync(Guid treinadorId, CancellationToken ct);
}
