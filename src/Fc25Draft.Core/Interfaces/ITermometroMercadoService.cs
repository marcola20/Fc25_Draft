using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Janela de mercado: ciclos próximos no tempo agrupados (os ciclos são pequenos e diários).</summary>
public record TermometroJanelaDto(int Numero, string Nome, DateTime Inicio, DateTime Fim, int Ciclos);

/// <summary>Um jogador que chegou ou saiu de um time na janela.</summary>
public record TermometroMovimentoDto(
    DateTime Data,
    // "Leilão", "Compra", "Venda", "Troca", "Empréstimo", "Venda rápida"…
    string Tipo,
    int PlayerId,
    string JogadorNome,
    string Posicao,
    int Overall,
    // O time do outro lado (de onde veio ou para onde foi); nulo no leilão e na venda rápida.
    Guid? OutroTimeId,
    string? OutroTimeNome,
    decimal? Valor);

/// <summary>Quem chegou e quem saiu de um time na janela.</summary>
public record TermometroChegadasSaidasDto(
    Guid TimeId,
    string TimeNome,
    IReadOnlyList<TermometroMovimentoDto> Chegadas,
    IReadOnlyList<TermometroMovimentoDto> Saidas);

public interface ITermometroMercadoService
{
    /// <summary>Janelas de mercado, da mais recente para a mais antiga.</summary>
    Task<IReadOnlyList<TermometroJanelaDto>> ListarJanelasAsync(CancellationToken ct);

    /// <summary>Termômetro de uma janela (pelo número) ou de todo o histórico (nulo).</summary>
    Task<TermometroMercadoDto> CalcularAsync(int? janela, CancellationToken ct);

    /// <summary>
    /// Quem chegou e quem saiu de cada time na janela (ou em todo o histórico), de todo tipo: leilão,
    /// compra e venda entre times, troca, empréstimo, venda rápida. Só times que mexeram no elenco.
    /// </summary>
    Task<IReadOnlyList<TermometroChegadasSaidasDto>> ChegadasESaidasAsync(int? janela, CancellationToken ct);
}
