namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Quanto vale um palpite. Cravar o placar vale 10; acertar quem ganhou e o número de gols
/// de um dos times vale 7; acertar só quem ganhou (ou que deu empate) vale 5.
/// <para>
/// Desde <see cref="RegrasNovasDesde"/>: placar invertido (chutou 2x1, deu 1x2) tira 3, e cada lado do
/// jogo tem uma aposta extra — o jogador escolhido fazer gol ou dar assistência ou, para o time em que
/// a pessoa chutou 0, o clean sheet do adversário. Acertou +3, errou −1; o jogador que nem entrou em
/// campo não conta.
/// </para>
/// </summary>
public static class BolaoPontuacao
{
    public const int PlacarExato = 10;
    public const int VencedorEUmPlacar = 7;
    public const int SoOVencedor = 5;
    public const int PlacarInvertido = -3;

    public const int ExtraAcerto = 3;
    public const int ExtraErro = -1;

    /// <summary>
    /// Os pontos são recalculados a cada vez; as regras novas valem só para os jogos marcados a partir
    /// deste dia (horário de Brasília), para não mexer no que já foi apurado.
    /// </summary>
    public static readonly DateTime RegrasNovasDesde = new(2026, 10, 14);

    public static int Calcular(int palpiteCasa, int palpiteFora, int golsCasa, int golsFora, bool regrasNovas = false)
    {
        if (palpiteCasa == golsCasa && palpiteFora == golsFora) return PlacarExato;

        if (regrasNovas && palpiteCasa != palpiteFora && palpiteCasa == golsFora && palpiteFora == golsCasa)
            return PlacarInvertido;

        if (Resultado(palpiteCasa, palpiteFora) != Resultado(golsCasa, golsFora)) return 0;

        var acertouUmLado = palpiteCasa == golsCasa || palpiteFora == golsFora;
        return acertouUmLado ? VencedorEUmPlacar : SoOVencedor;
    }

    /// <summary>
    /// A aposta extra de um lado do jogo. Chutou 0 para o time: aposta que ele passa em branco (clean sheet
    /// do adversário). Senão, o jogador escolhido — nulo quando não escolheu ninguém ou quando ele não entrou.
    /// </summary>
    public static int? Extra(int palpiteDoLado, int golsDoLado, bool escolheuJogador, bool participou, bool entrou)
    {
        if (palpiteDoLado == 0) return golsDoLado == 0 ? ExtraAcerto : ExtraErro;
        if (!escolheuJogador) return null;
        if (participou) return ExtraAcerto;
        return entrou ? ExtraErro : null;
    }

    /// <summary>1 casa, -1 fora, 0 empate.</summary>
    private static int Resultado(int casa, int fora) => casa.CompareTo(fora);

    public static string Explicacao(int pontos) => pontos switch
    {
        PlacarExato => "Cravou o placar",
        VencedorEUmPlacar => "Acertou o vencedor e um dos placares",
        SoOVencedor => "Acertou o resultado",
        PlacarInvertido => "Placar invertido",
        _ => "Errou"
    };
}
