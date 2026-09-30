using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Pesos e parâmetros do Power Ranking. Ficam aqui para a página mostrar
/// exatamente os mesmos critérios usados no cálculo.
/// </summary>
public static class PowerRankingCriterios
{
    // Pesos de cada nota (0 a 100) na nota final. Somam 100.
    public const int PesoElenco = 40;
    public const int PesoRating = 40;
    public const int PesoForma = 20;

    /// <summary>Força do elenco = média de overall dos 11 melhores jogadores.</summary>
    public const int JogadoresNoXI = 11;

    /// <summary>Forma = aproveitamento nos últimos jogos (todas as competições).</summary>
    public const int JogosNaForma = 5;

    // Rating Elo: todo time começa com 1500; ganhar de time mais forte rende mais.
    public const int RatingInicial = 1500;
    public const int K = 30;

    /// <summary>Na virada de temporada o rating volta essa fração do caminho até 1500 (o elenco muda no draft).</summary>
    public const double RegressaoTemporada = 1.0 / 3;

    /// <summary>
    /// Multiplicador do K pela diferença de gols (mesmo esquema do ranking Elo de seleções):
    /// 1 gol = 1; 2 gols = 1,5; 3 gols = 1,75; daí em diante +0,125 por gol.
    /// </summary>
    public static double MultiplicadorMargem(int diferenca) => diferenca switch
    {
        <= 1 => 1.0,
        2 => 1.5,
        3 => 1.75,
        _ => 1.75 + (diferenca - 3) / 8.0
    };
}

/// <summary>Jogo encerrado (sem W.O.). <paramref name="Dia"/> é a data do jogo no horário de Brasília.</summary>
public record PowerRankingPartidaInput(
    Guid CasaId, Guid ForaId, int GolsCasa, int GolsFora, int Temporada, DateOnly Dia, DateTime Ordem);

/// <summary>Time que entra no ranking, com o overall de cada jogador do elenco atual.</summary>
public record PowerRankingTimeInput(Guid TimeId, string Nome, Divisao? Divisao, IReadOnlyList<int> Overalls);

public static class PowerRanking
{
    /// <summary>
    /// Calcula o ranking com todos os jogos e, para mostrar quem subiu e quem caiu,
    /// de novo sem os jogos do último dia com jogo. O elenco é o atual nas duas contas,
    /// então a variação vem só dos resultados.
    /// </summary>
    public static PowerRankingDto Calcular(IReadOnlyList<PowerRankingTimeInput> times, IReadOnlyList<PowerRankingPartidaInput> partidas)
    {
        var ordenadas = partidas.OrderBy(p => p.Ordem).ToList();
        var atual = Ordenar(times, ordenadas);

        DateOnly? ultimoDia = ordenadas.Count == 0 ? null : ordenadas.Max(p => p.Dia);
        var anteriores = ordenadas.Where(p => p.Dia < ultimoDia).ToList();
        Dictionary<Guid, int>? posicaoAnterior = anteriores.Count == 0
            ? null
            : Ordenar(times, anteriores).ToDictionary(x => x.TimeId, x => x.Posicao);

        var itens = atual
            .Select(x => x with
            {
                Variacao = posicaoAnterior is not null && posicaoAnterior.TryGetValue(x.TimeId, out var antes)
                    ? antes - x.Posicao
                    : null
            })
            .ToList();

        return new PowerRankingDto(itens, ultimoDia, posicaoAnterior is null ? null : anteriores.Max(p => p.Dia));
    }

    private static List<PowerRankingItemDto> Ordenar(IReadOnlyList<PowerRankingTimeInput> times, IReadOnlyList<PowerRankingPartidaInput> partidas)
    {
        var ratings = CalcularRatings(partidas);
        var ids = times.Select(t => t.TimeId).ToHashSet();

        var linhas = times.Select(t =>
        {
            var xi = t.Overalls.OrderByDescending(o => o).Take(PowerRankingCriterios.JogadoresNoXI).ToList();
            var forcaXI = xi.Count == 0 ? 0 : xi.Average();
            var rating = ratings.GetValueOrDefault(t.TimeId, PowerRankingCriterios.RatingInicial);
            var forma = Forma(t.TimeId, partidas);
            return (Time: t, ForcaXI: forcaXI, Rating: rating, Forma: forma);
        }).ToList();

        // Elenco e rating viram nota relativa: o melhor do grupo tem 100 e o pior 0.
        // A forma já é um aproveitamento (0 a 100).
        var notaElenco = Escala(linhas.Select(l => l.ForcaXI).ToList());
        var notaRating = Escala(linhas.Select(l => l.Rating).ToList());

        return linhas
            .Select((l, i) =>
            {
                var notaForma = NotaForma(l.Forma);
                var nota = (notaElenco[i] * PowerRankingCriterios.PesoElenco
                            + notaRating[i] * PowerRankingCriterios.PesoRating
                            + notaForma * PowerRankingCriterios.PesoForma) / 100.0;
                return new PowerRankingItemDto(
                    0, l.Time.TimeId, l.Time.Nome, l.Time.Divisao,
                    Math.Round(nota, 1), null,
                    Math.Round(l.ForcaXI, 1), Math.Round(notaElenco[i], 1),
                    (int)Math.Round(l.Rating), Math.Round(notaRating[i], 1),
                    l.Forma, Math.Round(notaForma, 1));
            })
            .OrderByDescending(x => x.Nota)
            .ThenByDescending(x => x.Rating)
            .ThenByDescending(x => x.ForcaXI)
            .ThenBy(x => x.TimeNome, StringComparer.CurrentCultureIgnoreCase)
            .Select((x, i) => x with { Posicao = i + 1 })
            .ToList();
    }

    /// <summary>
    /// Elo de todos os jogos, na ordem em que foram disputados. Pênaltis contam como empate
    /// (vale o placar do tempo normal), igual ao resto do site.
    /// </summary>
    private static Dictionary<Guid, double> CalcularRatings(IReadOnlyList<PowerRankingPartidaInput> partidas)
    {
        var ratings = new Dictionary<Guid, double>();
        double De(Guid id) => ratings.GetValueOrDefault(id, PowerRankingCriterios.RatingInicial);
        int? temporada = null;

        foreach (var p in partidas)
        {
            // Só regride quando começa uma temporada nova: jogo atrasado da anterior
            // (ex.: Supercopa depois da estreia da Série B) não conta como virada.
            if (temporada is int anterior && p.Temporada > anterior)
            {
                foreach (var id in ratings.Keys.ToList())
                    ratings[id] -= (ratings[id] - PowerRankingCriterios.RatingInicial) * PowerRankingCriterios.RegressaoTemporada;
            }
            if (temporada is null || p.Temporada > temporada)
                temporada = p.Temporada;

            var casa = De(p.CasaId);
            var fora = De(p.ForaId);
            var esperado = 1 / (1 + Math.Pow(10, (fora - casa) / 400));
            var resultado = p.GolsCasa > p.GolsFora ? 1.0 : p.GolsCasa < p.GolsFora ? 0.0 : 0.5;
            var delta = PowerRankingCriterios.K
                        * PowerRankingCriterios.MultiplicadorMargem(Math.Abs(p.GolsCasa - p.GolsFora))
                        * (resultado - esperado);

            ratings[p.CasaId] = casa + delta;
            ratings[p.ForaId] = fora - delta;
        }

        return ratings;
    }

    /// <summary>Últimos resultados ("V", "E" ou "D"), do mais recente para o mais antigo.</summary>
    private static IReadOnlyList<string> Forma(Guid timeId, IReadOnlyList<PowerRankingPartidaInput> partidas) =>
        partidas
            .Where(p => p.CasaId == timeId || p.ForaId == timeId)
            .Reverse()
            .Take(PowerRankingCriterios.JogosNaForma)
            .Select(p =>
            {
                var pro = p.CasaId == timeId ? p.GolsCasa : p.GolsFora;
                var contra = p.CasaId == timeId ? p.GolsFora : p.GolsCasa;
                return pro > contra ? "V" : pro < contra ? "D" : "E";
            })
            .ToList();

    /// <summary>
    /// Aproveitamento nos últimos jogos. Os que faltam para completar a conta valem 50,
    /// para um time com 1 jogo só não ir direto a 0 ou 100.
    /// </summary>
    private static double NotaForma(IReadOnlyList<string> forma)
    {
        var jogados = forma.Sum(r => r == "V" ? 100.0 : r == "E" ? 100.0 / 3 : 0);
        var faltando = PowerRankingCriterios.JogosNaForma - forma.Count;
        return (jogados + faltando * 50.0) / PowerRankingCriterios.JogosNaForma;
    }

    private static double[] Escala(IReadOnlyList<double> valores)
    {
        if (valores.Count == 0)
            return Array.Empty<double>();

        var min = valores.Min();
        var max = valores.Max();
        return max - min < 1e-9
            ? valores.Select(_ => 50.0).ToArray()
            : valores.Select(v => (v - min) * 100 / (max - min)).ToArray();
    }
}
