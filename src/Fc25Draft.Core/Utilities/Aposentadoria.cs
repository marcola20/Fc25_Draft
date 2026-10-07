namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Regras da aposentadoria (ver docs/ciclo-de-vida-dos-jogadores.md). Ficam aqui para a tela mostrar os mesmos
/// números usados no sorteio.
/// </summary>
public static class AposentadoriaCriterios
{
    /// <summary>Chance de anunciar a despedida pela idade na temporada. (idade, chance); a partir da última, sempre.</summary>
    public static readonly IReadOnlyList<(int Idade, double Chance)> Chances =
    [
        (34, 0.10),
        (35, 0.25),
        (36, 0.50),
        (37, 0.75),
        (38, 1.00),
    ];

    /// <summary>Goleiro dura mais: conta como se tivesse estes anos a menos (igual à curva de evolução).</summary>
    public const int AnosAMenosDoGoleiro = 3;

    // Quem ainda joga em alto nível adia a despedida (não vale para quem já está na idade de "sempre").
    public const int OverallAlto = 88;
    public const double FatorOverallAlto = 0.5;
    public const int OverallBom = 85;
    public const double FatorOverallBom = 0.75;
}

public static class Aposentadoria
{
    /// <summary>Chance de anunciar a despedida na temporada (0 a 1).</summary>
    public static double Chance(int idade, bool goleiro, int overall)
    {
        var idadeDaTabela = goleiro ? idade - AposentadoriaCriterios.AnosAMenosDoGoleiro : idade;
        var tabela = AposentadoriaCriterios.Chances;
        if (idadeDaTabela < tabela[0].Idade) return 0;
        if (idadeDaTabela >= tabela[^1].Idade) return 1;

        var chance = tabela.Last(l => idadeDaTabela >= l.Idade).Chance;
        if (overall >= AposentadoriaCriterios.OverallAlto) chance *= AposentadoriaCriterios.FatorOverallAlto;
        else if (overall >= AposentadoriaCriterios.OverallBom) chance *= AposentadoriaCriterios.FatorOverallBom;
        return chance;
    }

    /// <summary>
    /// Sorteio estável: o mesmo jogador na mesma temporada tira sempre o mesmo número, então a prévia não muda a cada
    /// vez que a tela abre.
    /// </summary>
    public static bool Sorteado(int temporada, int playerId, double chance)
    {
        if (chance <= 0) return false;
        if (chance >= 1) return true;
        var semente = unchecked(temporada * 100_003 + playerId * 7_919);
        return new Random(semente).NextDouble() < chance;
    }
}
