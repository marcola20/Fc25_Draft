using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Interfaces;

/// <summary>Treino da semana (<see cref="TreinoCriterios"/>).</summary>
public interface ITreinoService
{
    /// <summary>O elenco com quem pode treinar, o treino desta semana e o resultado da semana passada.</summary>
    Task<TreinoDoTimeDto> DoTimeAsync(Guid teamId, CancellationToken ct);

    /// <summary>Troca o treino desta semana do time pelas <paramref name="escolhas"/> (até 3; vazio limpa).</summary>
    Task SalvarAsync(Guid teamId, IReadOnlyList<TreinoEscolhaRequest> escolhas, CancellationToken ct);

    /// <summary>Faz o treino das semanas que já fecharam. Retorna quantos jogadores treinaram.</summary>
    Task<int> TreinarSemanasFechadasAsync(CancellationToken ct);

    /// <summary>A partir de sexta ao meio-dia, lembra quem ainda não montou o treino da semana. Retorna quantos avisos.</summary>
    Task<int> LembrarAsync(CancellationToken ct);

    /// <summary>Os treinos do jogador na temporada atual (no clube em que ele está).</summary>
    Task<IReadOnlyList<TreinoResultadoDto>> DoJogadorAsync(int playerId, CancellationToken ct);

    /// <summary>
    /// Forma o par de padrinho e afilhado do time na temporada: um veterano e um jovem que pode treinar, da mesma
    /// posição. Um par por temporada; se um dos dois saiu do clube, dá para formar outro.
    /// </summary>
    Task ApadrinharAsync(Guid teamId, int padrinhoId, int afilhadoId, CancellationToken ct);

    /// <summary>Par de quem é padrinho ou afilhado no clube atual (nulo se não é).</summary>
    Task<PadrinhoDto?> PadrinhoDoJogadorAsync(int playerId, CancellationToken ct);

    /// <summary>Notícias do Plantão: quem se formou no treino (passou do overall do treino).</summary>
    Task<IReadOnlyList<PlantaoNoticiaDto>> GetNoticiasAsync(CancellationToken ct);
}
