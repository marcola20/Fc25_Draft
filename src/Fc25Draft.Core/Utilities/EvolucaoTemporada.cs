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
        (30, 0),
        (33, -1),
        (36, -2),
        (int.MaxValue, -3),
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

    /// <summary>Ninguém sobe mais que isso numa temporada (idade + desempenho + explosão).</summary>
    public const int SubidaMaxima = 3;

    /// <summary>Quem tem clube nunca cai mais que isso numa temporada. O livre veterano cai até <see cref="QuedaMaximaSemClube"/>.</summary>
    public const int QuedaMaximaComClube = 2;
    public const int QuedaMaximaSemClube = 3;

    /// <summary>Livre com até essa idade fica guardado para o draft: não sobe nem cai.</summary>
    public const int LivreParadoAte = 24;

    /// <summary>
    /// Os pontos da evolução vão para os atributos: cada ponto é ±1 em 3 ou 4 atributos (sorteado por jogador e
    /// temporada) e o overall é o que a fórmula der.
    /// </summary>
    public const int AtributosPorPontoMin = 3;
    public const int AtributosPorPontoMax = 4;

    // Surpresas: fora da curva, sorteadas por jogador e temporada (o admin cancela na prévia). Explosão só com clube.
    public const int ExplosaoAte = 25;            // explosão é mais comum até essa idade
    public const int QuedaDeRendimentoDesde = 29; // queda de rendimento é mais comum a partir dessa idade
    public const double ChanceSurpresaComum = 0.06;
    public const double ChanceSurpresaRara = 0.02;
    public const int SurpresaMin = 1;
    public const int SurpresaMax = 2;

    /// <summary>Na queda mexe em um pouco mais de atributos: os físicos e mais alguns dos que pesam na posição.</summary>
    public const int AtributosNaQuedaMin = 4;
    public const int AtributosNaQuedaMax = 5;

    /// <summary>
    /// Goleiro só tem 5 atributos de goleiro, que carregam quase todo o overall: com 2 por ponto anda como o de um
    /// jogador de linha.
    /// </summary>
    public const int AtributosDoGoleiro = 2;
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
/// <param name="Piso">Queda máxima (negativo); nulo = sem limite.</param>
/// <param name="Surpresa">Explosão (+) ou queda de rendimento (−) sorteada; 0 = nenhuma.</param>
/// <param name="Teto">Subida máxima; nulo = sem limite.</param>
public record VariacaoCalculada(int Curva, int Desempenho, IReadOnlyList<string> Motivos, int? Piso = null, int Surpresa = 0, int? Teto = null)
{
    public int Total => Math.Clamp(Curva + Desempenho + Surpresa, Piso ?? int.MinValue, Teto ?? int.MaxValue);
}

public static class EvolucaoTemporada
{
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static int Curva(int idade, bool goleiro)
    {
        var idadeDaTabela = goleiro ? idade - EvolucaoCriterios.AnosAMenosDoGoleiro : idade;
        return EvolucaoCriterios.Curva.First(l => idadeDaTabela <= l.Ate).Variacao;
    }

    /// <param name="surpresa">Explosão (+) ou queda de rendimento (−) sorteada para o jogador; 0 = nenhuma.</param>
    public static VariacaoCalculada Calcular(int idade, bool goleiro, DesempenhoTemporada d, int surpresa = 0)
    {
        var motivos = new List<string>();
        var curva = Curva(idade, goleiro);
        motivos.Add($"idade {idade}{(goleiro ? " (goleiro)" : "")}: {Sinal(curva)}");

        // Livre jovem fica guardado para o draft do jeito que está.
        if (d.SemClube && idade <= EvolucaoCriterios.LivreParadoAte)
        {
            motivos.Add($"livre até {EvolucaoCriterios.LivreParadoAte} anos, guardado para o draft: não muda");
            return new VariacaoCalculada(curva, 0, motivos, Piso: 0, Teto: 0);
        }

        var desempenho = 0;
        var pct = d.PercentualTitular;

        if (d.SemClube)
        {
            desempenho--;
            motivos.Add("sem clube: −1");
            // Sem jogar não tem explosão.
            if (surpresa > 0) surpresa = 0;
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

        // Livre nunca sobe; a queda tem limite (maior para o livre veterano encostado); a subida também.
        var soma = curva + desempenho + surpresa;
        var piso = -(d.SemClube ? EvolucaoCriterios.QuedaMaximaSemClube : EvolucaoCriterios.QuedaMaximaComClube);
        var teto = d.SemClube ? 0 : EvolucaoCriterios.SubidaMaxima;
        if (soma < piso)
            motivos.Add($"limite de queda{(d.SemClube ? " sem clube" : " com clube")}: {Sinal(piso)}");
        else if (soma > teto)
            motivos.Add(d.SemClube ? "livre não sobe: 0" : $"limite de subida: {Sinal(teto)}");

        return new VariacaoCalculada(curva, desempenho, motivos, piso, surpresa, teto);
    }

    /// <summary>
    /// Surpresa da temporada (sorteio estável por jogador): explosão de +1 a +2, mais comum até 25 anos; ou queda de
    /// rendimento de −1 a −2, mais comum a partir de 29. Na maioria dos casos, 0.
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
