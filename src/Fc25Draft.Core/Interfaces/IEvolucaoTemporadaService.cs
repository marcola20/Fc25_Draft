using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Evolução de fim de temporada: cada jogador sobe ou cai pela idade (curva G) e pelo desempenho na temporada.
/// As mudanças viram atributos e vão para o save pelo Editor PES (EvolucaoPes).
/// </summary>
public interface IEvolucaoTemporadaService
{
    /// <summary>
    /// A conta de cada jogador; se a temporada já foi aplicada, o que foi aplicado. <paramref name="surpresasCanceladas"/>:
    /// jogadores cuja surpresa sorteada o admin tirou.
    /// </summary>
    Task<EvolucaoPreviaDto> GetPreviaAsync(int temporada, CancellationToken ct, IReadOnlyCollection<int>? surpresasCanceladas = null);

    /// <summary>Aplica a evolução da temporada (uma vez só): atributos, overall e as EvolucaoPes para o save.</summary>
    Task<EvolucaoPreviaDto> AplicarAsync(int temporada, CancellationToken ct, IReadOnlyCollection<int>? surpresasCanceladas = null);

    /// <summary>
    /// Ajuste manual do admin, a qualquer momento: ± pontos nos atributos (como a evolução) com um motivo. Vai para o save
    /// pelo Editor PES e aparece no gráfico da ficha. Devolve o que mudou ("Finalização +2, …").
    /// </summary>
    Task<string> AjustarAsync(int playerId, int pontos, string motivo, CancellationToken ct);

    /// <summary>Temporadas em que a evolução já foi aplicada, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<int>> ListTemporadasAplicadasAsync(CancellationToken ct);

    /// <summary>A última evolução de fim de temporada do jogador; nulo se ele ainda não passou por nenhuma.</summary>
    Task<VariacaoJogadorDto?> GetUltimaDoJogadorAsync(int playerId, CancellationToken ct);

    /// <summary>Desfaz a evolução da temporada, enquanto o Editor PES não tiver gravado nada dela no jogo.</summary>
    Task<EvolucaoPreviaDto> DesfazerAsync(int temporada, CancellationToken ct);
}
