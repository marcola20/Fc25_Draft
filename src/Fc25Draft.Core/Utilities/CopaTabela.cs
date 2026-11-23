using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// A tabela da primeira fase da Copa, nos dois formatos: grupos (os 2 primeiros de cada grupo vão às
/// quartas) ou fase de liga (tabela única: 1º ao 4º às quartas, 5º ao 12º ao playoff). As telas usam
/// isto para ordenar, numerar e colorir do mesmo jeito que o admin decide quem passa.
/// </summary>
public static class CopaTabela
{
    public enum Faixa { Quartas, Playoff, Eliminado }

    public static List<T> Ordenar<T>(
        IEnumerable<T> itens, bool faseDeLiga, Func<T, Guid> timeId, Func<T, DesempateStats> stats, JogosDecisivos? decisivos) =>
        faseDeLiga
            ? LigaDesempate.OrdenarFaseDeLiga(itens, timeId, stats, decisivos)
            : LigaDesempate.Ordenar(itens, TipoCompetition.Copa, timeId, stats, Array.Empty<ConfrontoDireto>(), decisivos);

    public static List<int> Posicoes<T>(
        IReadOnlyList<T> ordenados, bool faseDeLiga, Func<T, Guid> timeId, Func<T, DesempateStats> stats, JogosDecisivos? decisivos) =>
        faseDeLiga
            ? LigaDesempate.PosicoesFaseDeLiga(ordenados, timeId, stats, decisivos)
            : LigaDesempate.PosicoesCopa(ordenados, timeId, stats, decisivos);

    /// <summary>
    /// Se a linha <paramref name="i"/> está num empate que vai a jogo decisivo. Nos grupos, qualquer
    /// empate; na fase de liga, só o que atravessa uma linha (4º/5º ou 12º/13º).
    /// </summary>
    public static bool EmDecisao(IReadOnlyList<int> posicoes, int i, bool faseDeLiga)
    {
        if (!faseDeLiga) return posicoes.Count(p => p == posicoes[i]) > 1;

        var inicio = posicoes.TakeWhile(p => p != posicoes[i]).Count();
        return LigaDesempate.EmpateNaLinha(posicoes, inicio);
    }

    public static Faixa FaixaDa(int posicao, bool faseDeLiga) =>
        faseDeLiga
            ? posicao <= LigaDesempate.LinhasDaFaseDeLiga[0] ? Faixa.Quartas
              : posicao <= LigaDesempate.LinhasDaFaseDeLiga[1] ? Faixa.Playoff
              : Faixa.Eliminado
            : posicao <= 2 ? Faixa.Quartas : Faixa.Eliminado;

    /// <summary>Texto da legenda de cada faixa.</summary>
    public static string Rotulo(Faixa faixa, bool faseDeLiga) => faixa switch
    {
        Faixa.Quartas => faseDeLiga ? "Quartas de final (1º ao 4º)" : "Avança (1º-2º do grupo)",
        Faixa.Playoff => "Playoff (5º ao 12º)",
        _ => "Eliminado"
    };

    /// <summary>Texto da legenda do empate que vai a jogo decisivo.</summary>
    public static string RotuloEmpate(bool faseDeLiga) => faseDeLiga
        ? "Empatados (Pts, V, SG e GP) na linha: jogo decisivo"
        : "Empatados (Pts, V e SG): jogo decisivo";

    /// <summary>Linha da tabela com as cores do Bootstrap (verde passa, amarelo playoff, vermelho fora; azul em decisão).</summary>
    public static string ClasseDaLinha(int posicao, bool emDecisao, bool faseDeLiga) =>
        emDecisao ? "table-primary" : FaixaDa(posicao, faseDeLiga) switch
        {
            Faixa.Quartas => "table-success",
            Faixa.Playoff => "table-warning",
            _ => "table-danger"
        };
}
