using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Web.Utilities;

/// <summary>Cores das zonas da classificação da Liga (linhas e badges do Bootstrap).</summary>
public static class ZonaEstilo
{
    public static string Linha(ZonaClassificacao zona) => zona switch
    {
        ZonaClassificacao.Campeao or ZonaClassificacao.CampeaoComAcesso or ZonaClassificacao.AcessoDireto => "table-success",
        ZonaClassificacao.PlayoffAcesso => "table-info",
        ZonaClassificacao.PlayoffRebaixamento => "table-warning",
        ZonaClassificacao.Rebaixamento => "table-danger",
        _ => ""
    };

    public static string Badge(ZonaClassificacao zona) => zona switch
    {
        ZonaClassificacao.Campeao or ZonaClassificacao.CampeaoComAcesso or ZonaClassificacao.AcessoDireto => "bg-success",
        ZonaClassificacao.PlayoffAcesso => "bg-info text-dark",
        ZonaClassificacao.PlayoffRebaixamento => "bg-warning text-dark",
        ZonaClassificacao.Rebaixamento => "bg-danger",
        _ => "bg-secondary"
    };

    /// <summary>Empate no topo em Pts, V, SG e GP: o título sai em jogo decisivo ou mini liga.</summary>
    public const string LinhaEmpateTopo = "table-primary";
    public const string BadgeEmpateTopo = "bg-primary";
}
