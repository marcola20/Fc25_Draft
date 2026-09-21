using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Números de um time usados nos critérios de desempate da classificação.</summary>
public readonly record struct DesempateStats(int Pontos, int Vitorias, int SaldoGols, int GolsPro);

/// <summary>Partida encerrada considerada no critério de confronto direto (Liga).</summary>
public readonly record struct ConfrontoDireto(Guid TimeCasaId, Guid TimeForaId, int GolsCasa, int GolsFora);

/// <summary>
/// Resultados dos jogos decisivos já encerrados. Na Copa, o vencedor fica à frente do
/// adversário com quem estava empatado, sem que o jogo renda pontos na classificação.
/// </summary>
public sealed class JogosDecisivos
{
    public static readonly JogosDecisivos Nenhum = new(Array.Empty<(Guid, Guid)>());

    private readonly HashSet<(Guid Vencedor, Guid Perdedor)> _resultados;

    public JogosDecisivos(IEnumerable<(Guid Vencedor, Guid Perdedor)> resultados) =>
        _resultados = resultados.ToHashSet();

    /// <summary>Já houve jogo decisivo encerrado entre os dois times.</summary>
    public bool Decidiu(Guid timeA, Guid timeB) =>
        _resultados.Contains((timeA, timeB)) || _resultados.Contains((timeB, timeA));

    public bool Venceu(Guid time, Guid adversario) => _resultados.Contains((time, adversario));

    public Guid? VencedorEntre(Guid timeA, Guid timeB) =>
        _resultados.Contains((timeA, timeB)) ? timeA
        : _resultados.Contains((timeB, timeA)) ? timeB
        : null;
}

/// <summary>
/// Critérios de desempate da classificação, compartilhados entre o recálculo oficial
/// e a simulação do admin.
/// <para>
/// <b>Liga:</b> Pontos → Vitórias → Saldo de Gols → Gols Pró → Confronto direto → jogo decisivo
/// (só nas posições que mudam de zona; empate no topo segue a decisão de campeão).
/// </para>
/// <para>
/// <b>Copa:</b> Pontos → Vitórias → Saldo de Gols. Quem empatar nesses três critérios
/// permanece empatado: a posição sai em jogo decisivo.
/// </para>
/// </summary>
public static class LigaDesempate
{
    /// <summary>Times empatados em Pontos, Vitórias e Saldo de Gols — jogo decisivo na Copa.</summary>
    public static bool EmpatadosNaCopa(DesempateStats a, DesempateStats b) =>
        a.Pontos == b.Pontos && a.Vitorias == b.Vitorias && a.SaldoGols == b.SaldoGols;

    /// <summary>
    /// Vencedor de um jogo decisivo encerrado: pelo placar ou, em caso de empate,
    /// pelo vencedor nos pênaltis. Null quando o jogo ainda não decidiu nada.
    /// </summary>
    public static Guid? VencedorDoJogoDecisivo(
        Guid timeCasaId, Guid timeForaId, int golsCasa, int golsFora,
        bool temPenaltis, Guid? penaltisVencedorId)
    {
        if (golsCasa > golsFora) return timeCasaId;
        if (golsFora > golsCasa) return timeForaId;
        if (temPenaltis && penaltisVencedorId is Guid vencedor) return vencedor;
        return null;
    }

    /// <summary>Monta os resultados a partir das partidas das rodadas de desempate.</summary>
    public static JogosDecisivos DosJogos(IEnumerable<LigaPartidaDto> partidas)
    {
        var resultados = new List<(Guid, Guid)>();

        foreach (var p in partidas.Where(x => x.Status == PartidaStatus.Encerrada))
        {
            var vencedor = VencedorDoJogoDecisivo(
                p.TimeCasaId, p.TimeForaId, p.GolsCasa, p.GolsFora, p.TemPenaltis, p.PenaltisVencedorId);

            if (vencedor is Guid v)
                resultados.Add((v, v == p.TimeCasaId ? p.TimeForaId : p.TimeCasaId));
        }

        return new JogosDecisivos(resultados);
    }

    public static List<T> Ordenar<T>(
        IEnumerable<T> itens,
        TipoCompetition tipo,
        Func<T, Guid> timeId,
        Func<T, DesempateStats> stats,
        IReadOnlyList<ConfrontoDireto> confrontos,
        JogosDecisivos? decisivos = null)
    {
        if (tipo == TipoCompetition.Copa)
        {
            // Copa não usa gols pró nem confronto direto: só o jogo decisivo separa
            // empatados; sem ele a ordem é apenas estável (por TimeId).
            var porCriterio = itens
                .OrderByDescending(x => stats(x).Pontos)
                .ThenByDescending(x => stats(x).Vitorias)
                .ThenByDescending(x => stats(x).SaldoGols)
                .ThenBy(timeId)
                .ToList();

            return decisivos is null
                ? porCriterio
                : PorBlocoEmpatado(porCriterio, stats, bloco => OrdenarPorJogoDecisivo(bloco, timeId, decisivos));
        }

        var ordenados = itens
            .OrderByDescending(x => stats(x).Pontos)
            .ThenByDescending(x => stats(x).Vitorias)
            .ThenByDescending(x => stats(x).SaldoGols)
            .ThenByDescending(x => stats(x).GolsPro)
            .ToList();

        var resultado = new List<T>(ordenados.Count);
        int i = 0;
        while (i < ordenados.Count)
        {
            var atual = stats(ordenados[i]);
            var empatados = ordenados
                .Skip(i)
                .TakeWhile(x => stats(x) == atual)
                .ToList();

            if (empatados.Count == 1)
            {
                resultado.AddRange(empatados);
            }
            else
            {
                // Confronto direto; quem segue igual nele só se separa por jogo decisivo (zonas da Liga).
                var parciais = ParciaisConfrontoDireto(empatados.Select(timeId), confrontos);
                var porConfronto = AplicarConfrontoDireto(empatados, timeId, parciais);

                resultado.AddRange(decisivos is null
                    ? porConfronto
                    : PorBloco(porConfronto, (a, b) => parciais[timeId(a)] == parciais[timeId(b)],
                        bloco => OrdenarPorJogoDecisivo(bloco, timeId, decisivos)));
            }

            i += empatados.Count;
        }

        return resultado;
    }

    /// <summary>
    /// Numeração de uma Liga já ordenada: times iguais em Pontos, Vitórias, Saldo, Gols Pró
    /// <b>e</b> no confronto direto entre eles dividem a posição — a menos que um jogo decisivo
    /// entre os dois já tenha sido disputado.
    /// </summary>
    public static List<int> PosicoesLiga<T>(
        IReadOnlyList<T> ordenados,
        Func<T, Guid> timeId,
        Func<T, DesempateStats> stats,
        IReadOnlyList<ConfrontoDireto> confrontos,
        JogosDecisivos? decisivos = null)
    {
        var posicoes = new List<int>(ordenados.Count);
        int i = 0;

        while (i < ordenados.Count)
        {
            var atual = stats(ordenados[i]);
            var bloco = ordenados.Skip(i).TakeWhile(x => stats(x) == atual).ToList();
            var parciais = ParciaisConfrontoDireto(bloco.Select(timeId), confrontos);

            for (int k = 0; k < bloco.Count; k++)
            {
                var empatadoComAnterior = k > 0
                    && parciais[timeId(bloco[k])] == parciais[timeId(bloco[k - 1])]
                    && decisivos?.Decidiu(timeId(bloco[k]), timeId(bloco[k - 1])) != true;

                posicoes.Add(empatadoComAnterior ? posicoes[^1] : i + k + 1);
            }

            i += bloco.Count;
        }

        return posicoes;
    }

    /// <summary>
    /// Numeração de um grupo da Copa já ordenado: times empatados em Pontos, Vitórias e
    /// Saldo de Gols dividem a mesma posição (ex.: 1, 2, 2, 4) — a menos que o jogo
    /// decisivo entre eles já tenha sido disputado, quando o vencedor assume a posição
    /// da frente.
    /// </summary>
    public static List<int> PosicoesCopa<T>(
        IReadOnlyList<T> ordenados,
        Func<T, Guid> timeId,
        Func<T, DesempateStats> stats,
        JogosDecisivos? decisivos = null)
    {
        var posicoes = new List<int>(ordenados.Count);

        for (int i = 0; i < ordenados.Count; i++)
        {
            var empatadoComAnterior = i > 0
                && EmpatadosNaCopa(stats(ordenados[i]), stats(ordenados[i - 1]))
                && decisivos?.Decidiu(timeId(ordenados[i]), timeId(ordenados[i - 1])) != true;

            posicoes.Add(empatadoComAnterior ? posicoes[i - 1] : i + 1);
        }

        return posicoes;
    }

    /// <summary>Aplica <paramref name="resolver"/> a cada bloco de times empatados em Pts/V/SG.</summary>
    private static List<T> PorBlocoEmpatado<T>(
        List<T> ordenados,
        Func<T, DesempateStats> stats,
        Func<List<T>, List<T>> resolver) =>
        PorBloco(ordenados, (a, b) => EmpatadosNaCopa(stats(a), stats(b)), resolver);

    /// <summary>Aplica <paramref name="resolver"/> a cada sequência de itens considerados iguais.</summary>
    private static List<T> PorBloco<T>(List<T> ordenados, Func<T, T, bool> iguais, Func<List<T>, List<T>> resolver)
    {
        var resultado = new List<T>(ordenados.Count);
        int i = 0;

        while (i < ordenados.Count)
        {
            var primeiro = ordenados[i];
            var bloco = ordenados.Skip(i).TakeWhile(x => iguais(x, primeiro)).ToList();

            resultado.AddRange(bloco.Count > 1 ? resolver(bloco) : bloco);
            i += bloco.Count;
        }

        return resultado;
    }

    /// <summary>Dentro do bloco empatado, quem venceu o jogo decisivo fica na frente.</summary>
    private static List<T> OrdenarPorJogoDecisivo<T>(List<T> bloco, Func<T, Guid> timeId, JogosDecisivos decisivos)
    {
        var ids = bloco.Select(timeId).ToList();

        return bloco
            .OrderByDescending(x => ids.Count(outro => outro != timeId(x) && decisivos.Venceu(timeId(x), outro)))
            .ThenBy(timeId)
            .ToList();
    }

    private static List<T> AplicarConfrontoDireto<T>(
        List<T> grupo,
        Func<T, Guid> timeId,
        Dictionary<Guid, (int Pts, int V, int SG, int GP)> parciais)
    {
        if (parciais.Values.Distinct().Count() <= 1)
            return grupo;

        return grupo
            .OrderByDescending(g => parciais[timeId(g)].Pts)
            .ThenByDescending(g => parciais[timeId(g)].V)
            .ThenByDescending(g => parciais[timeId(g)].SG)
            .ThenByDescending(g => parciais[timeId(g)].GP)
            .ThenBy(timeId)
            .ToList();
    }

    /// <summary>Mini tabela só com os jogos entre os times empatados.</summary>
    private static Dictionary<Guid, (int Pts, int V, int SG, int GP)> ParciaisConfrontoDireto(
        IEnumerable<Guid> times,
        IReadOnlyList<ConfrontoDireto> confrontos)
    {
        var ids = times.ToHashSet();
        var h2h = confrontos
            .Where(p => ids.Contains(p.TimeCasaId) && ids.Contains(p.TimeForaId))
            .ToList();

        var parciais = ids.ToDictionary(id => id, _ => (Pts: 0, V: 0, SG: 0, GP: 0));

        foreach (var p in h2h)
        {
            var (ptsCasa, vCasa, sgCasa, gpCasa) = parciais[p.TimeCasaId];
            var (ptsFora, vFora, sgFora, gpFora) = parciais[p.TimeForaId];

            gpCasa += p.GolsCasa;
            gpFora += p.GolsFora;
            sgCasa += p.GolsCasa - p.GolsFora;
            sgFora += p.GolsFora - p.GolsCasa;

            if (p.GolsCasa > p.GolsFora) { ptsCasa += 3; vCasa += 1; }
            else if (p.GolsCasa == p.GolsFora) { ptsCasa += 1; ptsFora += 1; }
            else { ptsFora += 3; vFora += 1; }

            parciais[p.TimeCasaId] = (ptsCasa, vCasa, sgCasa, gpCasa);
            parciais[p.TimeForaId] = (ptsFora, vFora, sgFora, gpFora);
        }

        return parciais;
    }
}
