using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Comissão técnica (<see cref="ComissaoCriterios"/>).</summary>
public interface IComissaoService
{
    Task<ComissaoDoTimeDto> DoTimeAsync(Guid teamId, CancellationToken ct);

    /// <summary>
    /// Contrata o cargo para a temporada em andamento: paga na hora, do caixa. Só nas primeiras semanas da temporada
    /// e uma vez por cargo.
    /// </summary>
    Task ContratarAsync(Guid teamId, CargoDaComissao cargo, CancellationToken ct);
}
