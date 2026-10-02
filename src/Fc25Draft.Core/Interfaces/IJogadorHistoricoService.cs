using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Jogo a jogo e evolução do overall, para a página do jogador.</summary>
public interface IJogadorHistoricoService
{
    /// <summary>Partidas que o jogador jogou, da mais recente para a mais antiga, com o resumo das notas.</summary>
    Task<JogadorJogosDto> JogosAsync(int playerId, CancellationToken ct);

    /// <summary>Mudanças de overall (venda rápida, evolução no PES…), da mais antiga para a atual.</summary>
    Task<IReadOnlyList<JogadorOverallPontoDto>> EvolucaoOverallAsync(int playerId, CancellationToken ct);
}
