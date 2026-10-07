using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Aposentadoria com aviso: numa temporada o jogador anuncia que é a última; na virada seguinte se aposenta, sai do
/// elenco e fica só no histórico. O Editor PES tira os aposentados dos clubes no save.
/// </summary>
public interface IAposentadoriaService
{
    Task<AposentadoriaPainelDto> GetPainelAsync(int temporada, CancellationToken ct);

    /// <summary>Esses jogadores anunciam que <paramref name="temporada"/> é a última deles.</summary>
    Task AnunciarAsync(int temporada, IReadOnlyCollection<int> playerIds, CancellationToken ct);

    /// <summary>O jogador desiste da despedida.</summary>
    Task CancelarAnuncioAsync(int playerId, CancellationToken ct);

    /// <summary>Aposenta quem anunciou a despedida em temporadas anteriores a <paramref name="temporada"/>. Retorna quantos.</summary>
    Task<int> AposentarAsync(int temporada, CancellationToken ct);

    /// <summary>Desfaz a aposentadoria: volta ao clube em que estava (ou fica livre se não der).</summary>
    Task DesaposentarAsync(int playerId, CancellationToken ct);

    /// <summary>Aposentados, para o Editor PES tirar dos clubes no save.</summary>
    Task<IReadOnlyList<AposentadoPesDto>> ListAposentadosParaPesAsync(CancellationToken ct);

    /// <summary>Notícias do Plantão: despedidas anunciadas e aposentadorias.</summary>
    Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct);
}
