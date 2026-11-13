using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Markdown simples dos anúncios do site, para o admin escrever sem HTML:
/// <list type="bullet">
/// <item>"## Título" vira um subtítulo;</item>
/// <item>"- item" e "1. item" viram listas;</item>
/// <item>linhas que começam com "&gt; " viram um quadro de exemplo (a primeira palavra em negrito, se terminar em ":");</item>
/// <item>linha em branco separa parágrafos;</item>
/// <item>*negrito* (ou **negrito**) e _itálico_, como no WhatsApp.</item>
/// </list>
/// O texto é escapado antes, então nenhum HTML digitado passa.
/// </summary>
public static partial class TextoDoSite
{
    public static string ParaHtml(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "";

        var html = new StringBuilder();
        var paragrafo = new List<string>();
        var exemplo = new List<string>();
        string? lista = null; // "ul" ou "ol" aberta

        void FecharParagrafo()
        {
            if (paragrafo.Count == 0) return;
            html.Append("<p>").Append(string.Join("<br>", paragrafo.Select(Inline))).Append("</p>");
            paragrafo.Clear();
        }

        void FecharExemplo()
        {
            if (exemplo.Count == 0) return;
            html.Append("<div class=\"texto-site-exemplo\">");
            foreach (var bloco in Paragrafos(exemplo))
                html.Append("<p>").Append(string.Join("<br>", bloco.Select(Inline))).Append("</p>");
            html.Append("</div>");
            exemplo.Clear();
        }

        void FecharLista()
        {
            if (lista is null) return;
            html.Append("</").Append(lista).Append('>');
            lista = null;
        }

        void FecharTudo()
        {
            FecharParagrafo();
            FecharExemplo();
            FecharLista();
        }

        foreach (var bruta in texto.Replace("\r\n", "\n").Split('\n'))
        {
            var linha = bruta.TrimEnd();

            if (linha.StartsWith('>'))
            {
                FecharParagrafo();
                FecharLista();
                exemplo.Add(linha[1..].TrimStart());
                continue;
            }
            FecharExemplo();

            if (linha.Length == 0)
            {
                FecharParagrafo();
                FecharLista();
                continue;
            }

            if (linha.StartsWith("## ") || linha.StartsWith("### "))
            {
                FecharTudo();
                html.Append("<h3>").Append(Inline(linha.TrimStart('#').Trim())).Append("</h3>");
                continue;
            }

            var item = ItemDeLista().Match(linha);
            if (item.Success)
            {
                FecharParagrafo();
                var tipo = item.Groups["num"].Success ? "ol" : "ul";
                if (lista != tipo)
                {
                    FecharLista();
                    html.Append('<').Append(tipo).Append('>');
                    lista = tipo;
                }
                html.Append("<li>").Append(Inline(item.Groups["texto"].Value)).Append("</li>");
                continue;
            }

            // Linha comum logo depois de um item: continua o parágrafo seguinte, a lista fecha.
            FecharLista();
            paragrafo.Add(linha.Trim());
        }

        FecharTudo();
        return html.ToString();
    }

    /// <summary>Texto corrido (sem marcação), para resumos e prévias de link.</summary>
    public static string SemMarcacao(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? "" : Marcas().Replace(texto, "$1$2").Trim();

    private static IEnumerable<List<string>> Paragrafos(List<string> linhas)
    {
        var atual = new List<string>();
        foreach (var l in linhas)
        {
            if (l.Length == 0)
            {
                if (atual.Count > 0) yield return atual;
                atual = new List<string>();
            }
            else atual.Add(l);
        }
        if (atual.Count > 0) yield return atual;
    }

    private static string Inline(string texto)
    {
        var html = WebUtility.HtmlEncode(texto);
        html = NegritoDuplo().Replace(html, "<strong>$1</strong>");
        html = Negrito().Replace(html, "<strong>$1</strong>");
        html = Italico().Replace(html, "<em>$1</em>");
        return html;
    }

    [GeneratedRegex(@"^\s*(?:(?<num>\d+)[.)]|[-•])\s+(?<texto>.+)$")]
    private static partial Regex ItemDeLista();

    [GeneratedRegex(@"\*\*(?=\S)(.+?)(?<=\S)\*\*")]
    private static partial Regex NegritoDuplo();

    [GeneratedRegex(@"(?<![\w*])\*(?=\S)([^*\n]*?\S)\*(?![\w*])")]
    private static partial Regex Negrito();

    [GeneratedRegex(@"(?<![\w_])_(?=\S)([^_\n]*?\S)_(?![\w_])")]
    private static partial Regex Italico();

    [GeneratedRegex(@"\*{1,2}([^*\n]+)\*{1,2}|(?<![\w_])_([^_\n]+)_(?![\w_])")]
    private static partial Regex Marcas();
}
