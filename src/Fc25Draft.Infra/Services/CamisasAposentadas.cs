using Fc25Draft.Infra.Data;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Camisas aposentadas: o número que um ídolo usava no clube em que se aposentou. Ninguém mais usa esse número lá
/// (aba Camisas e envio do Editor PES). Sai das linhas de ídolo e do jogador, sem tabela própria: desfazer a
/// aposentadoria devolve o número.
/// </summary>
internal static class CamisasAposentadas
{
    public sealed record Linha(Guid TimeId, short Numero, int JogadorId, string Nome);

    /// <summary>As camisas aposentadas dos clubes pedidos (o filtro vem antes de montar a linha, para o EF traduzir).</summary>
    public static IQueryable<Linha> DosTimes(DraftDbContext db, IReadOnlyCollection<Guid> times) =>
        db.Idolos
            .Where(i => times.Contains(i.TimeId)
                        && i.Jogador.AposentadoEm != null && i.Jogador.TimeAoSeAposentar == i.TimeId
                        && i.Jogador.NumeroAoSeAposentar != null)
            .Select(i => new Linha(i.TimeId, i.Jogador.NumeroAoSeAposentar!.Value, i.JogadorId, i.Jogador.Name));
}
