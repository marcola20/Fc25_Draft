using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

public static class LigaLabels
{
    public static string DivisaoNome(Divisao divisao) => divisao switch
    {
        Divisao.SerieA => "Série A",
        Divisao.SerieB => "Série B",
        _ => divisao.ToString()
    };

    /// <summary>Rótulo curto da competição: "Série A", "Série B", "Liga" (sem divisão), "Copa" ou "Supercopa".</summary>
    public static string Competicao(TipoCompetition tipo, Divisao? divisao) => tipo switch
    {
        TipoCompetition.Liga => divisao is null ? "Liga" : DivisaoNome(divisao.Value),
        TipoCompetition.Copa => "Copa",
        TipoCompetition.Supercopa => "Supercopa",
        _ => tipo.ToString()
    };
}
