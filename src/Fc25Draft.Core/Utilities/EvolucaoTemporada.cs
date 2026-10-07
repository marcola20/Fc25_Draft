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
    public const decimal NotaBoa = 7.0m;       // média ≥: +1
    public const decimal NotaRuim = 6.0m;      // média <: −1 (com jogos suficientes)
    public const int JogosParaNotaRuim = 5;

    /// <summary>Quem tem clube nunca cai mais que isso numa temporada (idade + desempenho). Livre pode cair mais.</summary>
    public const int QuedaMaximaComClube = 3;
}

/// <summary>
/// O que o jogador fez na temporada: jogos do clube (com escalação gravada, desde que ele chegou), quantos
/// começou como titular e a nota média do PES.
/// </summary>
public record DesempenhoTemporada(bool SemClube, int JogosDoClube, int Titular, decimal? NotaMedia, int JogosComNota)
{
    public double? PercentualTitular => JogosDoClube > 0 ? Math.Min(1.0, (double)Titular / JogosDoClube) : null;
}

/// <summary>A conta da variação de um jogador: curva + desempenho, com o porquê de cada parte.</summary>
/// <param name="Piso">Queda máxima (negativo) para quem tem clube; nulo = sem limite.</param>
public record VariacaoCalculada(int Curva, int Desempenho, IReadOnlyList<string> Motivos, int? Piso = null)
{
    public int Total => Piso is int piso ? Math.Max(Curva + Desempenho, piso) : Curva + Desempenho;
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
    public static VariacaoCalculada Calcular(int idade, bool goleiro, DesempenhoTemporada d, int? idadeMaximaDraft = null)
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

        // Com clube, a queda tem limite; livre (veterano encostado) pode cair mais.
        int? piso = d.SemClube ? null : -EvolucaoCriterios.QuedaMaximaComClube;
        if (piso is int limite && curva + desempenho < limite)
            motivos.Add($"limite de queda com clube: {Sinal(limite)}");

        return new VariacaoCalculada(curva, desempenho, motivos, piso);
    }

    private static string Pct(double v) => $"{Math.Round(v * 100):0}%";

    public static string Sinal(int v) => v > 0 ? $"+{v}" : v < 0 ? $"−{-v}" : "0";
}
