using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface ITransferConfigService
{
    /// <summary>Retorna a configuração atual, criando os padrões na primeira vez.</summary>
    Task<TransferConfigDto> GetAsync(CancellationToken ct);

    /// <summary>Persiste os limites de transferências.</summary>
    Task<TransferConfigDto> UpdateAsync(TransferConfigDto dto, CancellationToken ct);
}
