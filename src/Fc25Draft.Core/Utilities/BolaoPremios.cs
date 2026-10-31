using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// O bolão paga o clube: quem faz mais pontos na rodada leva dinheiro para o caixa do clube que treina, e os três
/// primeiros do geral da temporada também. Quem está sem clube joga, mas não recebe.
/// </summary>
public static class BolaoPremios
{
    /// <summary>Os prêmios começam nesta temporada (as anteriores ficam só no ranking).</summary>
    public const int ValeAPartirDaTemporada = 2011;

    /// <summary>Para quem faz mais pontos na rodada (empate divide).</summary>
    public const decimal PorRodada = 2_000_000m;

    /// <summary>Do 1º ao 3º do geral da temporada.</summary>
    public static readonly IReadOnlyList<decimal> Geral = [15_000_000m, 10_000_000m, 5_000_000m];

    /// <summary>
    /// Quem leva quanto. <paramref name="premios"/> é o prêmio de cada colocação (1º, 2º, ...). Empatados em pontos e
    /// cravadas (o desempate do ranking) dividem igualmente a soma das colocações que ocupam. Sem ponto, sem prêmio.
    /// </summary>
    public static IReadOnlyList<(BolaoRankingLinhaDto Linha, int Posicao, decimal Valor)> Rateio(
        IReadOnlyList<BolaoRankingLinhaDto> ranking, IReadOnlyList<decimal> premios)
    {
        var resultado = new List<(BolaoRankingLinhaDto, int, decimal)>();
        var grupos = ranking.Where(l => l.Pontos > 0)
            .OrderByDescending(l => l.Pontos).ThenByDescending(l => l.Cravadas)
            .GroupBy(l => (l.Pontos, l.Cravadas));

        var posicao = 1;
        foreach (var grupo in grupos)
        {
            if (posicao > premios.Count) break;
            var empatados = grupo.ToList();
            var soma = premios.Skip(posicao - 1).Take(empatados.Count).Sum();
            var cada = decimal.Round(soma / empatados.Count, 2, MidpointRounding.ToZero);
            resultado.AddRange(empatados.Select(l => (l, posicao, cada)));
            posicao += empatados.Count;
        }
        return resultado;
    }

    /// <summary>"R$ 2 mi", "R$ 666,67 mil".</summary>
    public static string Rotulo(decimal valor) =>
        valor >= 1_000_000m
            ? $"R$ {valor / 1_000_000m:0.##} mi".Replace('.', ',')
            : $"R$ {valor / 1_000m:0.##} mil".Replace('.', ',');
}
