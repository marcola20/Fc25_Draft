using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Utilities;

/// <summary>Time na tabela da sua divisão. Na Série B, quem segue empatado divide a posição (1, 2, 2, 4).</summary>
public sealed record TimeNaTabela(Guid TimeId, string TimeNome, int Posicao);

/// <summary>Campanha de um time na fase de grupos da Copa.</summary>
public sealed record TimeNaCopa(
    Guid TimeId, string TimeNome, GrupoCopa Grupo, int PosicaoNoGrupo,
    int Pontos, int Jogos, int SaldoGols, int GolsPro)
{
    public double Aproveitamento => Jogos == 0 ? 0 : Pontos / (3.0 * Jogos);
}

/// <summary>Quem caiu em cada fase do mata-mata da Copa.</summary>
public sealed record ChaveCopa(
    IReadOnlyList<Guid> EliminadosQuartas,
    IReadOnlyList<Guid> EliminadosSemis,
    Guid Vice,
    Guid Campeao);

/// <summary>Uma pick sorteada. <paramref name="Origem"/> é o que a loteria mostra (ex.: "8º da Série B").</summary>
public sealed record PickLoteria(int Pick, Guid TimeId, string TimeNome, int Posicao, string Origem);

/// <summary>
/// Loteria do draft com Série A (10) e Série B (8), a regra da aba "Próximo draft" de /picks.
/// Round 1: a Série B escolhe antes da A; do 3º ao 8º da B disputam as picks 1–6 por loteria
/// ponderada, os demais em 50/50 por dupla. Round 2: só a campanha na Copa, sem separar divisão.
/// </summary>
public static class LoteriaDraft
{
    public const int TimesSerieA = 10;
    public const int TimesSerieB = 8;
    public const int TimesCopa = TimesSerieA + TimesSerieB;

    /// <summary>Round 1: as picks 1–6 saem da loteria da Série B.</summary>
    public const int PicksSorteadasRound1 = 6;

    /// <summary>Chance da pick #1 do 8º, 7º, 6º, 5º, 4º e 3º da Série B.</summary>
    public static readonly IReadOnlyList<int> PesosSerieB = [25, 20, 17, 15, 13, 10];

    /// <summary>Chance da primeira pick do bloco: do pior para o melhor eliminado do bloco.</summary>
    public static readonly IReadOnlyList<int> PesosGruposCopa = [30, 30, 15, 15, 10];

    public static List<PickLoteria> Round1(
        IReadOnlyList<TimeNaTabela> serieA, IReadOnlyList<TimeNaTabela> serieB, Random rng)
    {
        if (serieA.Count != TimesSerieA)
            throw new InvalidOperationException($"A Série A precisa ter {TimesSerieA} times (tem {serieA.Count}).");
        if (serieB.Count != TimesSerieB)
            throw new InvalidOperationException($"A Série B precisa ter {TimesSerieB} times (tem {serieB.Count}).");

        var a = serieA.OrderBy(t => t.Posicao).ToList();
        var b = serieB.OrderBy(t => t.Posicao).ToList();

        // 1º e 2º da B ficam fora da loteria: um empate que atravesse essa linha precisa ser resolvido antes.
        if (b[2].Posicao != 3)
            throw new InvalidOperationException(
                "A Série B tem empate sem solução entre o 2º e o 3º lugar. Resolva o desempate antes da loteria.");

        var picks = new List<PickLoteria>();

        // Picks 1–6: do 3º ao 8º da B. Empatados dividem a posição e ficam com a média das chances das vagas que ocupam.
        var sorteio = b.Skip(2)
            .Select(t => (Time: t, Peso: PesoSerieB(t, b)))
            .ToList();
        foreach (var t in SortearOrdem(sorteio, rng))
            picks.Add(Pick(picks.Count + 1, t, $"{t.Posicao}º da Série B"));

        // Picks 7–8: 1º e 2º da B no 50/50.
        AdicionarDupla(picks, b[1], b[0], "Série B", rng);

        // Picks 9–18: duplas da Série A, de baixo para cima (9º/10º, 7º/8º ... 1º/2º).
        for (int i = TimesSerieA - 1; i > 0; i -= 2)
            AdicionarDupla(picks, a[i], a[i - 1], "Série A", rng);

        return picks;
    }

    public static List<PickLoteria> Round2(IReadOnlyList<TimeNaCopa> copa, ChaveCopa chave, Random rng)
    {
        if (copa.Count != TimesCopa)
            throw new InvalidOperationException($"A Copa precisa ter {TimesCopa} times (tem {copa.Count}).");
        if (chave.EliminadosQuartas.Count != 4)
            throw new InvalidOperationException("A Copa precisa ter os 4 eliminados nas quartas.");
        if (chave.EliminadosSemis.Count != 2)
            throw new InvalidOperationException("A Copa precisa ter os 2 eliminados nas semis.");

        var porId = copa.ToDictionary(t => t.TimeId);
        TimeNaCopa Time(Guid id) => porId.TryGetValue(id, out var t)
            ? t
            : throw new InvalidOperationException("Time do mata-mata não está na classificação da Copa.");

        var naChave = chave.EliminadosQuartas.Concat(chave.EliminadosSemis).Append(chave.Vice).Append(chave.Campeao).ToHashSet();

        // Eliminados nos grupos: 5ºs, depois 4ºs, depois 3ºs; dentro da mesma posição, pior aproveitamento primeiro.
        var eliminados = copa
            .Where(t => !naChave.Contains(t.TimeId))
            .OrderByDescending(t => t.PosicaoNoGrupo)
            .ThenBy(t => t.Aproveitamento)
            .ThenBy(t => t.SaldoGols)
            .ThenBy(t => t.GolsPro)
            .ThenBy(_ => rng.Next())
            .ToList();
        if (eliminados.Count != 2 * PesosGruposCopa.Count)
            throw new InvalidOperationException(
                $"A Copa precisa ter {2 * PesosGruposCopa.Count} eliminados na fase de grupos (tem {eliminados.Count}).");

        var picks = new List<PickLoteria>();

        // Dois blocos de 5: picks 1–5 para os 5 piores, 6–10 para os 5 seguintes.
        foreach (var bloco in eliminados.Chunk(PesosGruposCopa.Count))
        {
            var sorteio = bloco.Select((t, i) => (Time: t, Peso: (double)PesosGruposCopa[i])).ToList();
            foreach (var t in SortearOrdem(sorteio, rng))
                picks.Add(new PickLoteria(picks.Count + 1, t.TimeId, t.TimeNome, t.PosicaoNoGrupo,
                    $"{t.PosicaoNoGrupo}º do Grupo {t.Grupo}"));
        }

        // Quartas e semis: pior aproveitamento na Copa escolhe antes.
        foreach (var (fase, ids) in new[] { ("Eliminado nas quartas", chave.EliminadosQuartas), ("Eliminado nas semis", chave.EliminadosSemis) })
        {
            var ordem = ids.Select(Time)
                .OrderBy(t => t.Aproveitamento)
                .ThenBy(t => t.SaldoGols)
                .ThenBy(t => t.GolsPro)
                .ThenBy(_ => rng.Next());
            foreach (var t in ordem)
                picks.Add(new PickLoteria(picks.Count + 1, t.TimeId, t.TimeNome, t.PosicaoNoGrupo, fase));
        }

        var vice = Time(chave.Vice);
        picks.Add(new PickLoteria(picks.Count + 1, vice.TimeId, vice.TimeNome, vice.PosicaoNoGrupo, "Vice-campeão da Copa"));
        var campeao = Time(chave.Campeao);
        picks.Add(new PickLoteria(picks.Count + 1, campeao.TimeId, campeao.TimeNome, campeao.PosicaoNoGrupo, "Campeão da Copa"));

        return picks;
    }

    /// <summary>Média das chances das vagas que o time ocupa (sozinho, só a da sua posição).</summary>
    private static double PesoSerieB(TimeNaTabela time, IReadOnlyList<TimeNaTabela> tabela)
    {
        var dividem = tabela.Count(t => t.Posicao == time.Posicao);
        return Enumerable.Range(time.Posicao, dividem)
            .Average(posicao => PesosSerieB[TimesSerieB - posicao]);
    }

    /// <summary>50/50: o sorteado fica com a primeira das duas picks.</summary>
    private static void AdicionarDupla(List<PickLoteria> picks, TimeNaTabela pior, TimeNaTabela melhor, string divisao, Random rng)
    {
        var (primeiro, segundo) = rng.Next(2) == 0 ? (pior, melhor) : (melhor, pior);
        picks.Add(Pick(picks.Count + 1, primeiro, $"{primeiro.Posicao}º da {divisao}"));
        picks.Add(Pick(picks.Count + 1, segundo, $"{segundo.Posicao}º da {divisao}"));
    }

    private static PickLoteria Pick(int numero, TimeNaTabela t, string origem) =>
        new(numero, t.TimeId, t.TimeNome, t.Posicao, origem);

    /// <summary>Sorteio ponderado em sequência, sem reposição: devolve a ordem das picks.</summary>
    public static List<T> SortearOrdem<T>(IReadOnlyList<(T Item, double Peso)> concorrentes, Random rng)
    {
        var pool = concorrentes.ToList();
        var ordem = new List<T>(pool.Count);

        while (pool.Count > 0)
        {
            var sorteio = rng.NextDouble() * pool.Sum(c => c.Peso);
            var idx = 0;
            for (double acumulado = 0; idx < pool.Count - 1; idx++)
            {
                acumulado += pool[idx].Peso;
                if (sorteio < acumulado) break;
            }

            ordem.Add(pool[idx].Item);
            pool.RemoveAt(idx);
        }

        return ordem;
    }
}
