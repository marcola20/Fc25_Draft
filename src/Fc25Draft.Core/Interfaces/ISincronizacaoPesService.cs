using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Troca de atributos entre o site e o save do PES (via Editor PES) e overall pela fórmula do PES.</summary>
public interface ISincronizacaoPesService
{
    /// <summary>Evoluções feitas no site (venda rápida...) que ainda não foram para o save, mais antigas primeiro.</summary>
    Task<IReadOnlyList<EvolucaoPesDto>> EvolucoesPendentesAsync(CancellationToken ct = default);

    /// <summary>
    /// Marca as evoluções aplicadas no jogo e grava no site os atributos que o jogo tem (somando as evoluções que
    /// ainda estão pendentes), recalculando o overall. Tudo ou nada: dado inválido gera ArgumentException;
    /// jogador apagado ou sem atributos no site só fica de fora (volta em Ignorados).
    /// </summary>
    Task<ResultadoSincronizacaoPesDto> SincronizarAsync(SincronizarPesDto dados, CancellationToken ct = default);

    /// <summary>
    /// Recalcula pela fórmula do PES o overall de todos os jogadores com atributos do jogo (os que têm a posição
    /// registrada no PES). Sem <paramref name="gravar"/>, só mostra o que mudaria.
    /// </summary>
    Task<IReadOnlyList<MudancaOverallDto>> RecalcularTodosAsync(bool gravar, CancellationToken ct = default);
}
