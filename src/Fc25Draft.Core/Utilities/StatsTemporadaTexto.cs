using System.Globalization;
using System.Text;

namespace Fc25Draft.Core.Utilities;

/// <summary>Um jogador num ranking da competição: o número dele e como o número aparece no texto.</summary>
public record StatJogador(string Nome, string TimeNome, decimal Valor);

/// <summary>
/// As estatísticas da competição do jeito que vão para o grupo: top 3 de artilharia, assistências,
/// clean sheets e nota. Quem empata fica junto na mesma linha (três jogadores com 3 gols em
/// terceiro saem os três no 🥉).
/// </summary>
public static class StatsTemporadaTexto
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly string[] Medalhas = { "🥇", "🥈", "🥉" };

    public static string Montar(
        string competicao,
        bool encerrada,
        IReadOnlyList<StatJogador> gols,
        IReadOnlyList<StatJogador> assistencias,
        IReadOnlyList<StatJogador> cleanSheets,
        IReadOnlyList<StatJogador> notas,
        int minimoDeJogosComNota)
    {
        var texto = new StringBuilder();

        texto.Append("📊 *ESTATÍSTICAS — ").Append(competicao.ToUpper(Br)).AppendLine("*");
        if (!encerrada)
            texto.AppendLine("_(competição em andamento)_");

        Secao(texto, "⚽ *ARTILHARIA*", gols, v => Contagem(v, "gol", "gols"));
        Secao(texto, "🅰️ *ASSISTÊNCIAS*", assistencias, v => Contagem(v, "assistência", "assistências"));
        Secao(texto, "🧤 *CLEAN SHEETS*", cleanSheets, v => Contagem(v, "jogo", "jogos"));

        var tituloNota = minimoDeJogosComNota > 1
            ? $"⭐ *MELHOR NOTA* _(mín. {minimoDeJogosComNota} jogos)_"
            : "⭐ *MELHOR NOTA*";
        Secao(texto, tituloNota, notas, v => v.ToString("0.00", Br));

        // Quebra de linha simples: o texto vai para o celular, não para um arquivo do Windows.
        return texto.ToString().Replace("\r\n", "\n").TrimEnd();
    }

    /// <summary>
    /// Top 3 como na tabela de esporte: quem empata divide a posição e o próximo pula
    /// (dois em 1º → o seguinte é 3º). O empate na 3ª posição entra inteiro.
    /// Valor zero não entra (ninguém fica em terceiro com 0 gols).
    /// </summary>
    public static IReadOnlyList<(int Posicao, decimal Valor, IReadOnlyList<StatJogador> Jogadores)> Top3(IEnumerable<StatJogador> ranking)
    {
        var top = new List<(int, decimal, IReadOnlyList<StatJogador>)>();
        var posicao = 1;

        foreach (var grupo in ranking.Where(j => j.Valor > 0).GroupBy(j => j.Valor).OrderByDescending(g => g.Key))
        {
            if (posicao > 3) break;

            var jogadores = grupo.OrderBy(j => j.Nome, StringComparer.Create(Br, ignoreCase: true)).ToList();
            top.Add((posicao, grupo.Key, jogadores));
            posicao += jogadores.Count;
        }

        return top;
    }

    private static void Secao(StringBuilder texto, string titulo, IReadOnlyList<StatJogador> ranking, Func<decimal, string> formatar)
    {
        var top = Top3(ranking);
        if (top.Count == 0) return;

        texto.AppendLine();
        texto.AppendLine(titulo);
        foreach (var (posicao, valor, jogadores) in top)
        {
            texto.Append(Medalhas[posicao - 1]).Append(' ')
                 .Append(string.Join(", ", jogadores.Select(j => $"{j.Nome} ({j.TimeNome})")))
                 .Append(" — ").AppendLine(formatar(valor));
        }
    }

    private static string Contagem(decimal valor, string singular, string plural)
    {
        var n = (int)valor;
        return $"{n} {(n == 1 ? singular : plural)}";
    }
}
