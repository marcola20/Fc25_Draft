using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Picks do próximo draft: de quem é cada uma (elas entram nas propostas entre times).</summary>
public interface IPicksDoDraftService
{
    /// <summary>Todas as picks do próximo draft (1ª e 2ª rodada de cada time) e o dono de cada uma.</summary>
    Task<PicksDoProximoDraftDto> ProximoDraftAsync(CancellationToken ct);

    /// <summary>Notícias do Plantão: picks que mudaram de dono.</summary>
    Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct);
}
