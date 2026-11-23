using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Textos dos prêmios, iguais na página pública, no admin e no extrato do caixa.</summary>
public static class PremiacaoLabels
{
    public static string Fase(FasePremiacao fase) => fase switch
    {
        FasePremiacao.Campeao => "Campeão",
        FasePremiacao.Vice => "Vice-campeão",
        FasePremiacao.Semifinal => "Eliminado nas semifinais",
        FasePremiacao.Quartas => "Eliminado nas quartas",
        FasePremiacao.Playoff => "Eliminado no playoff",
        FasePremiacao.FaseDeGrupos => "Eliminado na primeira fase",
        _ => fase.ToString()
    };

    /// <summary>Da fase mais longe (campeão) para a primeira; o playoff fica entre as quartas e a primeira fase.</summary>
    public static int Ordem(FasePremiacao fase) => fase switch
    {
        FasePremiacao.Playoff => 45,
        _ => (int)fase * 10
    };

    /// <summary>"3º lugar" para uma posição só, "4º ao 1º" para uma faixa, "3º em diante" quando é aberta.</summary>
    public static string Posicao(int? de, int? ate, int? totalTimes = null)
    {
        if (de is null) return "";
        if (ate is null || ate == de) return $"{de}º lugar";
        if (totalTimes is null || ate < totalTimes) return $"{de}º ao {ate}º lugar";
        return $"Do {de}º em diante";
    }

    /// <summary>Rótulo curto para o cabeçalho de um bloco: "Série A", "Série B", "Copa", "Supercopa".</summary>
    public static string Competicao(TipoCompetition tipo, Divisao? divisao) => LigaLabels.Competicao(tipo, divisao);
}
