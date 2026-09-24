using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Janela de mercado: ciclos próximos no tempo agrupados (os ciclos são pequenos e diários).</summary>
public record TermometroJanelaDto(int Numero, string Nome, DateTime Inicio, DateTime Fim, int Ciclos);

public interface ITermometroMercadoService
{
    /// <summary>Janelas de mercado, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<TermometroJanelaDto>> ListarJanelasAsync(CancellationToken ct);

    /// <summary>Termômetro de uma janela (pelo número) ou de todo o histórico (nulo).</summary>
    Task<TermometroMercadoDto> CalcularAsync(int? janela, CancellationToken ct);
}
