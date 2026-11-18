using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Utilities;

/// <summary>Uma premiação de estatística: o dinheiro do 1º, 2º e 3º (todos levam +1 over).</summary>
public record RegraEstatistica(CategoriaEstatistica Categoria, string Nome, string Posicao, IReadOnlyList<decimal> Valores)
{
    public decimal ValorDa(int posicao) => posicao is >= 1 and <= 3 ? Valores[posicao - 1] : 0m;
}

/// <summary>
/// Premiações individuais de estatística: top 3 de artilharia, assistências e clean sheets de cada competição.
/// A ordem da lista é a ordem do over: quem entra em duas fica com o over da primeira e repassa o outro.
/// </summary>
public static class PremiosEstatistica
{
    private const decimal Mi = 1_000_000m;

    /// <summary>Quem recebe um over repassado precisa estar com overall até este.</summary>
    public const int OverallMaximoDoRepasse = 87;

    public static readonly IReadOnlyList<RegraEstatistica> Todas = new[]
    {
        new RegraEstatistica(CategoriaEstatistica.Artilharia, "Artilharia", "artilheiro", new[] { 8 * Mi, 6 * Mi, 4 * Mi }),
        new RegraEstatistica(CategoriaEstatistica.Assistencias, "Assistências", "em assistências", new[] { 6 * Mi, 4 * Mi, 2 * Mi }),
        new RegraEstatistica(CategoriaEstatistica.CleanSheets, "Clean sheets", "em clean sheets", new[] { 6 * Mi, 4 * Mi, 2 * Mi }),
    };

    public static RegraEstatistica De(CategoriaEstatistica categoria) => Todas.First(r => r.Categoria == categoria);

    /// <summary>"vice-artilheiro", "3º em assistências", "artilheiro".</summary>
    public static string Titulo(CategoriaEstatistica categoria, int posicao) => (categoria, posicao) switch
    {
        (CategoriaEstatistica.Artilharia, 1) => "artilheiro",
        (CategoriaEstatistica.Artilharia, 2) => "vice-artilheiro",
        (CategoriaEstatistica.Artilharia, _) => $"{posicao}º artilheiro",
        _ => $"{posicao}º {De(categoria).Posicao}"
    };

    /// <summary>
    /// Top 3 como na tabela de esporte (igual ao texto do zap): empatados dividem a posição e o próximo pula
    /// (dois em 2º → o seguinte é 4º, fora). O empate na 3ª posição entra inteiro. Zero não entra.
    /// </summary>
    public static IReadOnlyList<(int Posicao, T Item)> Top3<T>(IEnumerable<T> ranking, Func<T, int> numero)
    {
        var top = new List<(int, T)>();
        var posicao = 1;

        foreach (var grupo in ranking.Where(x => numero(x) > 0).GroupBy(numero).OrderByDescending(g => g.Key))
        {
            if (posicao > 3) break;

            var itens = grupo.ToList();
            top.AddRange(itens.Select(i => (posicao, i)));
            posicao += itens.Count;
        }

        return top;
    }
}
