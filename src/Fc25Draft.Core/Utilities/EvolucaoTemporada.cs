using System.Globalization;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Regras da evolução de fim de temporada (ver docs/ciclo-de-vida-dos-jogadores.md). Ficam aqui para a prévia e
/// as telas mostrarem exatamente os mesmos critérios usados na conta.
/// </summary>
public static class EvolucaoCriterios
{
    /// <summary>Curva G: variação base pela idade na temporada. (idade até, variação)</summary>
    public static readonly IReadOnlyList<(int Ate, int Variacao)> Curva =
    [
        (20, +2),
        (23, +1),
        (27, 0),
        (29, -1),
        (31, -2),
        (33, -3),
        (int.MaxValue, -4),
    ];

    /// <summary>Goleiro envelhece mais devagar: usa a linha de quem tem 3 anos a menos.</summary>
    public const int AnosAMenosDoGoleiro = 3;

    // Desempenho na temporada (somado à curva).
    public const double TitularMuito = 0.60;   // mais que isso: +1
    public const double TitularPouco = 0.20;   // menos que isso (ou sem clube): −1
    public const int JogosParaTitular = 5;     // a regra do titular só vale com pelo menos esses jogos do clube
    public const decimal NotaBoa = 7.0m;       // média ≥: +1
    public const decimal NotaRuim = 6.0m;      // média <: −1 (com jogos suficientes)
    public const int JogosParaNotaRuim = 5;

    /// <summary>Quem tem clube nunca cai mais que isso numa temporada (idade + desempenho). Livre pode cair mais.</summary>
    public const int QuedaMaximaComClube = 3;

    /// <summary>
    /// Os pontos da evolução vão para os atributos: cada ponto é ±1 em 5 ou 6 atributos (sorteado por jogador e
    /// temporada) e o overall é o que a fórmula der.
    /// </summary>
    public const int AtributosPorPontoMin = 5;
    public const int AtributosPorPontoMax = 6;

    // Surpresas: fora da curva, sorteadas por jogador e temporada (o admin cancela na prévia).
    public const int ExplosaoAte = 25;            // explosão é mais comum até essa idade
    public const int QuedaDeRendimentoDesde = 29; // queda de rendimento é mais comum a partir dessa idade
    public const double ChanceSurpresaComum = 0.06;
    public const double ChanceSurpresaRara = 0.02;
    public const int SurpresaMin = 2;
    public const int SurpresaMax = 4;

    /// <summary>Na queda mexe em mais atributos: os físicos e mais alguns dos que pesam na posição.</summary>
    public const int AtributosNaQuedaMin = 7;
    public const int AtributosNaQuedaMax = 8;

    /// <summary>
    /// Goleiro só tem 5 atributos de goleiro: com 4 por ponto o overall anda como o de um jogador de linha
    /// (medido com o elenco real: +2 pontos ≈ +1,6 nos dois).
    /// </summary>
    public const int AtributosDoGoleiro = 4;
}

/// <summary>
/// O que o jogador fez na temporada: jogos do clube (com escalação gravada, desde que ele chegou), quantos
/// começou como titular e a nota média do PES.
/// </summary>
public record DesempenhoTemporada(bool SemClube, int JogosDoClube, int Titular, decimal? NotaMedia, int JogosComNota)
{
    public double? PercentualTitular => JogosDoClube > 0 ? Math.Min(1.0, (double)Titular / JogosDoClube) : null;
}

/// <summary>A conta da variação de um jogador: curva + desempenho (+ surpresa), com o porquê de cada parte.</summary>
/// <param name="Piso">Queda máxima (negativo) para quem tem clube; nulo = sem limite.</param>
/// <param name="Surpresa">Explosão (+) ou queda de rendimento (−) sorteada; 0 = nenhuma.</param>
public record VariacaoCalculada(int Curva, int Desempenho, IReadOnlyList<string> Motivos, int? Piso = null, int Surpresa = 0)
{
    public int Total => Piso is int piso ? Math.Max(Curva + Desempenho + Surpresa, piso) : Curva + Desempenho + Surpresa;
}

public static class EvolucaoTemporada
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static int Curva(int idade, bool goleiro)
    {
        var idadeDaTabela = goleiro ? idade - EvolucaoCriterios.AnosAMenosDoGoleiro : idade;
        return EvolucaoCriterios.Curva.First(l => idadeDaTabela <= l.Ate).Variacao;
    }

    /// <param name="idadeMaximaDraft">Livre com até essa idade está reservado para o draft: não perde por estar sem clube.</param>
    /// <param name="surpresa">Explosão (+) ou queda de rendimento (−) sorteada para o jogador; 0 = nenhuma.</param>
    public static VariacaoCalculada Calcular(int idade, bool goleiro, DesempenhoTemporada d, int? idadeMaximaDraft = null, int surpresa = 0)
    {
        var motivos = new List<string>();
        var curva = Curva(idade, goleiro);
        motivos.Add($"idade {idade}{(goleiro ? " (goleiro)" : "")}: {Sinal(curva)}");

        var desempenho = 0;
        var pct = d.PercentualTitular;

        if (d.SemClube)
        {
            if (idadeMaximaDraft is int maxima && idade <= maxima)
            {
                motivos.Add("livre, reservado para o draft: 0");
            }
            else
            {
                desempenho--;
                motivos.Add("sem clube: −1");
            }
        }
        else if (d.JogosDoClube < EvolucaoCriterios.JogosParaTitular)
        {
            // Poucos jogos não dizem nada (ex.: chegou no fim da temporada).
        }
        else if (pct is double p && p > EvolucaoCriterios.TitularMuito)
        {
            desempenho++;
            motivos.Add($"titular em {Pct(p)}: +1");
        }
        else if (pct is double q && q < EvolucaoCriterios.TitularPouco)
        {
            desempenho--;
            motivos.Add($"titular em {Pct(q)}: −1");
        }

        if (d.NotaMedia is decimal nota)
        {
            if (nota >= EvolucaoCriterios.NotaBoa)
            {
                desempenho++;
                motivos.Add($"nota {nota.ToString("0.0", Br)}: +1");
            }
            else if (nota < EvolucaoCriterios.NotaRuim && d.JogosComNota >= EvolucaoCriterios.JogosParaNotaRuim)
            {
                desempenho--;
                motivos.Add($"nota {nota.ToString("0.0", Br)}: −1");
            }
        }

        if (surpresa > 0) motivos.Add($"💥 explosão: {Sinal(surpresa)}");
        else if (surpresa < 0) motivos.Add($"📉 queda de rendimento: {Sinal(surpresa)}");

        // Com clube, a queda tem limite; livre (veterano encostado) pode cair mais.
        int? piso = d.SemClube ? null : -EvolucaoCriterios.QuedaMaximaComClube;
        if (piso is int limite && curva + desempenho + surpresa < limite)
            motivos.Add($"limite de queda com clube: {Sinal(limite)}");

        return new VariacaoCalculada(curva, desempenho, motivos, piso, surpresa);
    }

    /// <summary>
    /// Surpresa da temporada (sorteio estável por jogador): explosão de +2 a +4, mais comum até 25 anos; ou queda de
    /// rendimento de −2 a −4, mais comum a partir de 29. Na maioria dos casos, 0.
    /// </summary>
    public static int Surpresa(int temporada, int playerId, int idade)
    {
        var sorteio = new Random(unchecked(temporada * 31_337 + playerId * 104_729));
        var chanceExplosao = idade <= EvolucaoCriterios.ExplosaoAte ? EvolucaoCriterios.ChanceSurpresaComum : EvolucaoCriterios.ChanceSurpresaRara;
        var chanceQueda = idade >= EvolucaoCriterios.QuedaDeRendimentoDesde ? EvolucaoCriterios.ChanceSurpresaComum : EvolucaoCriterios.ChanceSurpresaRara;
        var x = sorteio.NextDouble();
        var tamanho = sorteio.Next(EvolucaoCriterios.SurpresaMin, EvolucaoCriterios.SurpresaMax + 1);
        if (x < chanceExplosao) return tamanho;
        if (x < chanceExplosao + chanceQueda) return -tamanho;
        return 0;
    }

    private static string Pct(double v) => $"{Math.Round(v * 100):0}%";

    /// <summary>
    /// Quantos atributos recebem os pontos: subindo 5 ou 6, caindo 7 ou 8 — sempre o mesmo para o jogador na temporada.
    /// </summary>
    public static int AtributosAfetados(int temporada, int playerId, int pontos, bool goleiro = false)
    {
        if (goleiro) return EvolucaoCriterios.AtributosDoGoleiro;
        var sorteio = new Random(unchecked(temporada * 92_821 + playerId * 6_007));
        return pontos < 0
            ? sorteio.Next(EvolucaoCriterios.AtributosNaQuedaMin, EvolucaoCriterios.AtributosNaQuedaMax + 1)
            : sorteio.Next(EvolucaoCriterios.AtributosPorPontoMin, EvolucaoCriterios.AtributosPorPontoMax + 1);
    }

    public static string Sinal(int v) => v > 0 ? $"+{v}" : v < 0 ? $"−{-v}" : "0";
}
