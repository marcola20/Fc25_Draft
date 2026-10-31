using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>
/// Papel no elenco (<see cref="PapelNoElenco"/>): a conversa que rebaixa o papel e a virada que refaz o de todos.
/// </summary>
public interface IPapelNoElencoService
{
    /// <summary>
    /// A conversa: o papel do jogador desce um degrau (titular → disputa → rotação) e o valor dele cai um pouco por
    /// alguns jogos. Uma por time por temporada; não vale para quem já pediu para sair.
    /// </summary>
    Task<PapelNoElenco> ConversarAsync(Guid teamId, Guid playerGuid, CancellationToken ct);

    /// <summary>
    /// Virada: liga a regra do papel (se ainda não estava) e refaz o papel de todos pelo que jogaram em
    /// <paramref name="temporadaQueAcabou"/>; as conversas da temporada acabam. Retorna quantos papéis gravou.
    /// </summary>
    Task<int> RefazerNaViradaAsync(int temporadaQueAcabou, CancellationToken ct);
}
