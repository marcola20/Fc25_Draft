using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IPricingConfigService
{
    /// <summary>Retorna a configuração atual, criando os padrões na primeira vez.</summary>
    Task<PricingConfigDto> GetAsync(CancellationToken ct);

    /// <summary>Persiste a configuração de precificação.</summary>
    Task<PricingConfigDto> UpdateAsync(PricingConfigDto dto, CancellationToken ct);

    /// <summary>
    /// Recalcula o comprar-agora dos itens em aberto (rascunho/ativo) dos ciclos especiais
    /// não encerrados com o fator especial atual. Pula item cujo lance atual já passa do novo valor.
    /// </summary>
    Task<(int Atualizados, int Pulados)> ReaplicarComprarJaEspecialAsync(CancellationToken ct);
}
