namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Quanto vale um palpite. Cravar o placar vale 10; acertar quem ganhou e o número de gols
/// de um dos times vale 7; acertar só quem ganhou (ou que deu empate) vale 5.
/// </summary>
public static class BolaoPontuacao
{
    public const int PlacarExato = 10;
    public const int VencedorEUmPlacar = 7;
    public const int SoOVencedor = 5;

    public static int Calcular(int palpiteCasa, int palpiteFora, int golsCasa, int golsFora)
    {
        if (palpiteCasa == golsCasa && palpiteFora == golsFora) return PlacarExato;

        if (Resultado(palpiteCasa, palpiteFora) != Resultado(golsCasa, golsFora)) return 0;

        var acertouUmLado = palpiteCasa == golsCasa || palpiteFora == golsFora;
        return acertouUmLado ? VencedorEUmPlacar : SoOVencedor;
    }

    /// <summary>1 casa, -1 fora, 0 empate.</summary>
    private static int Resultado(int casa, int fora) => casa.CompareTo(fora);

    public static string Explicacao(int pontos) => pontos switch
    {
        PlacarExato => "Cravou o placar",
        VencedorEUmPlacar => "Acertou o vencedor e um dos placares",
        SoOVencedor => "Acertou o resultado",
        _ => "Errou"
    };
}
