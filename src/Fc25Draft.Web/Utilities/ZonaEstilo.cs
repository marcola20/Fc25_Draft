using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Web.Utilities;

/// <summary>
/// Cores das zonas da classificação. A linha ganha só um traço colorido à esquerda (classe linha-zona) e o
/// número da posição vira um quadradinho na cor da zona; as cores ficam no site.css (--zona-*).
/// </summary>
public static class ZonaEstilo
{
    private static string Classe(ZonaClassificacao zona) => zona switch
    {
        ZonaClassificacao.Campeao or ZonaClassificacao.CampeaoComAcesso or ZonaClassificacao.AcessoDireto => "zona-titulo",
        ZonaClassificacao.PlayoffAcesso => "zona-playoff-acesso",
        ZonaClassificacao.PlayoffRebaixamento => "zona-playoff-rebaixamento",
        ZonaClassificacao.Rebaixamento => "zona-rebaixamento",
        _ => ""
    };

    public static string Linha(ZonaClassificacao zona) => Classe(zona) is { Length: > 0 } c ? $"linha-zona {c}" : "";

    public static string Badge(ZonaClassificacao zona) => Classe(zona) is { Length: > 0 } c ? $"posicao {c}" : "posicao zona-neutra";

    /// <summary>Empate no topo em Pts, V, SG e GP: o título sai em jogo decisivo ou mini liga.</summary>
    public const string LinhaEmpateTopo = "linha-zona zona-empate";
    public const string BadgeEmpateTopo = "posicao zona-empate";
}
