namespace Fc25Draft.Core.Utilities;

/// <summary>
/// A lista fixa de esquemas que a pessoa escolhe no perfil, com a posição de cada jogador para desenhar o
/// campinho. Coordenadas de 0 a 100: X da esquerda para a direita, Y do gol de baixo (goleiro) para o
/// ataque em cima.
/// </summary>
public static class EsquemasTaticos
{
    public sealed record Posicao(double X, double Y, string Sigla);

    public sealed record Esquema(string Nome, IReadOnlyList<Posicao> Posicoes);

    private static readonly Posicao Goleiro = new(50, 92, "GOL");

    /// <summary>
    /// Uma linha de jogadores espalhados por igual: quanto mais gente na linha, mais ela abre (dois
    /// atacantes ficam perto do meio; cinco defensores vão de lateral a lateral).
    /// </summary>
    private static IEnumerable<Posicao> Linha(double y, params string[] siglas)
    {
        var n = siglas.Length;
        var largura = n <= 1 ? 0 : Math.Min(76, 20 + 14 * (n - 1));
        for (var i = 0; i < n; i++)
        {
            var x = n == 1 ? 50 : 50 - largura / 2.0 + largura * i / (n - 1);
            yield return new Posicao(Math.Round(x, 1), y, siglas[i]);
        }
    }

    private static Esquema Montar(string nome, params IEnumerable<Posicao>[] linhas) =>
        new(nome, new[] { Goleiro }.Concat(linhas.SelectMany(l => l)).ToList());

    public static IReadOnlyList<Esquema> Todos { get; } = new[]
    {
        Montar("4-3-3", Linha(74, "LE", "ZAG", "ZAG", "LD"), Linha(50, "MC", "VOL", "MC"), Linha(22, "PE", "CA", "PD")),
        Montar("4-4-2", Linha(74, "LE", "ZAG", "ZAG", "LD"), Linha(48, "ME", "MC", "MC", "MD"), Linha(22, "CA", "CA")),
        Montar("4-2-3-1", Linha(74, "LE", "ZAG", "ZAG", "LD"), Linha(56, "VOL", "VOL"), Linha(36, "PE", "MEI", "PD"), Linha(16, "CA")),
        Montar("3-5-2", Linha(74, "ZAG", "ZAG", "ZAG"), Linha(50, "ME", "MC", "VOL", "MC", "MD"), Linha(22, "CA", "CA")),
        Montar("3-4-3", Linha(74, "ZAG", "ZAG", "ZAG"), Linha(50, "ME", "MC", "MC", "MD"), Linha(22, "PE", "CA", "PD")),
        Montar("5-3-2", Linha(74, "LE", "ZAG", "ZAG", "ZAG", "LD"), Linha(48, "MC", "VOL", "MC"), Linha(22, "CA", "CA")),
        Montar("4-1-2-1-2", Linha(74, "LE", "ZAG", "ZAG", "LD"), Linha(60, "VOL"), Linha(46, "MC", "MC"), Linha(32, "MEI"), Linha(16, "CA", "CA")),
        Montar("4-5-1", Linha(74, "LE", "ZAG", "ZAG", "LD"), Linha(46, "ME", "MC", "VOL", "MC", "MD"), Linha(18, "CA")),
        Montar("4-1-4-1", Linha(74, "LE", "ZAG", "ZAG", "LD"), Linha(58, "VOL"), Linha(40, "ME", "MC", "MC", "MD"), Linha(18, "CA")),
        Montar("5-4-1", Linha(74, "LE", "ZAG", "ZAG", "ZAG", "LD"), Linha(46, "ME", "MC", "MC", "MD"), Linha(18, "CA"))
    };

    private static readonly Dictionary<string, Esquema> PorNome = Todos.ToDictionary(e => e.Nome, StringComparer.OrdinalIgnoreCase);

    /// <summary>O esquema da lista com esse nome, ou nulo.</summary>
    public static Esquema? Achar(string? nome) =>
        string.IsNullOrWhiteSpace(nome) ? null : PorNome.GetValueOrDefault(nome.Trim());
}
