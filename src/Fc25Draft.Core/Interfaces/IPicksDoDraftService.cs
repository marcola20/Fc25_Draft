using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Picks do próximo draft: de quem é cada uma (elas entram nas propostas entre times).</summary>
public interface IPicksDoDraftService
{
    /// <summary>Picks dos drafts abertos (o próximo e um que esteja acontecendo), com dono, posição e proteção.</summary>
    Task<PicksDoProximoDraftDto> ProximoDraftAsync(CancellationToken ct);

    /// <summary>
    /// Depois da loteria, a pick protegida que caiu dentro da proteção volta para quem protegeu (que devolve o
    /// combinado). Retorna quantas voltaram.
    /// </summary>
    Task<int> ResolverProtecoesAsync(CancellationToken ct);

    /// <summary>Notícias do Plantão: picks que mudaram de dono.</summary>
    Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct);
}
