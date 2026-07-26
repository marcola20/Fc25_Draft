using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IPricingConfigService
{
    /// <summary>Retorna a configuração atual, criando os padrões na primeira vez.</summary>
    Task<PricingConfigDto> GetAsync(CancellationToken ct);

    /// <summary>Persiste a configuração de precificação.</summary>
    Task<PricingConfigDto> UpdateAsync(PricingConfigDto dto, CancellationToken ct);
}
