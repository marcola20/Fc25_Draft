using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Premiações individuais de estatística (top 3 de artilharia, assistências e clean sheets de cada competição:
/// Série A, Série B e Copa):
/// o admin dá tudo de uma vez — dinheiro para o clube e +1 over para o jogador. Quem entra em duas fica com o over
/// da primeira e o treinador do clube escolhe, na Minha Área, o companheiro que recebe o outro.
/// </summary>
public interface IPremiosEstatisticaService
{
    /// <summary>Quem leva o quê na competição: o que já foi dado ou, se ainda não, o que vai ser dado.</summary>
    Task<PremiosEstatisticaDto> GetAsync(Guid ligaId, CancellationToken ct);

    /// <summary>Paga o clube e dá o +1 over de cada premiado; repasses ficam para o treinador escolher. Devolve o resumo.</summary>
    Task<string> DarAsync(Guid ligaId, CancellationToken ct);

    /// <summary>
    /// Tira o dinheiro e os overs da competição — ou, se a temporada tem prêmios da regra antiga (temporada somada),
    /// os dela (só enquanto o Editor PES não levou nenhum para o save).
    /// </summary>
    Task<string> DesfazerAsync(Guid ligaId, CancellationToken ct);

    /// <summary>Companheiros de clube do premiado com overall até 87, para receber o over repassado.</summary>
    Task<IReadOnlyList<PremioJogadorOpcaoDto>> CompanheirosAsync(Guid premioId, CancellationToken ct);

    /// <summary>
    /// O admin dá o over que ficou faltando: o do próprio premiado (quando não deu sozinho) ou, no repasse, o do
    /// companheiro escolhido no lugar do treinador.
    /// </summary>
    Task<string> DarOverAsync(Guid premioId, int? paraPlayerId, CancellationToken ct);

    // ── Treinador (Minha Área) ─────────────────────────────────────────────

    /// <summary>Overs para repassar que esperam a escolha do treinador do clube.</summary>
    Task<IReadOnlyList<RepasseDeOverDto>> RepassesPendentesAsync(Guid teamId, CancellationToken ct);

    /// <summary>O treinador escolhe o companheiro (o token precisa ser do clube do premiado).</summary>
    Task<string> RepassarPeloTreinadorAsync(string? token, Guid premioId, int paraPlayerId, CancellationToken ct);
}
