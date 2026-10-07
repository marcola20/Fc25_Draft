using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Evolução de fim de temporada: cada jogador sobe ou cai pela idade (curva G) e pelo desempenho na temporada.
/// As mudanças viram atributos e vão para o save pelo Editor PES (EvolucaoPes).
/// </summary>
public interface IEvolucaoTemporadaService
{
    /// <summary>A conta de cada jogador; se a temporada já foi aplicada, o que foi aplicado.</summary>
    Task<EvolucaoPreviaDto> GetPreviaAsync(int temporada, CancellationToken ct);

    /// <summary>Aplica a evolução da temporada (uma vez só): atributos, overall e as EvolucaoPes para o save.</summary>
    Task<EvolucaoPreviaDto> AplicarAsync(int temporada, CancellationToken ct);

    /// <summary>Desfaz a evolução da temporada, enquanto o Editor PES não tiver gravado nada dela no jogo.</summary>
    Task<EvolucaoPreviaDto> DesfazerAsync(int temporada, CancellationToken ct);
}
