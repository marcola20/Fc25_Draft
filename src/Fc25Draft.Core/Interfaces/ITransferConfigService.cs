using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ITransferConfigService
{
    /// <summary>Retorna a configuração atual, criando os padrões na primeira vez.</summary>
    Task<TransferConfigDto> GetAsync(CancellationToken ct);

    /// <summary>Persiste os limites de transferências.</summary>
    Task<TransferConfigDto> UpdateAsync(TransferConfigDto dto, CancellationToken ct);

    /// <summary>Lista o contador de vendas rápidas de cada time, ordenado por nome.</summary>
    Task<IReadOnlyList<TeamQuickSellStatusDto>> GetQuickSellStatusAsync(CancellationToken ct);

    /// <summary>Zera o contador de vendas rápidas (quick sell) de todos os times. Retorna a quantidade de times afetados.</summary>
    Task<int> ResetQuickSellCountsAsync(CancellationToken ct);

    /// <summary>Lista o tamanho do elenco e o mínimo temporário de cada time, ordenado por nome.</summary>
    Task<IReadOnlyList<TeamElencoMinimoDto>> ListElencoMinimoAsync(CancellationToken ct);

    /// <summary>Define (ou remove, com nulo) o mínimo de elenco temporário de um time.</summary>
    Task SetElencoMinimoTemporarioAsync(Guid teamId, int? minimo, CancellationToken ct);
}
