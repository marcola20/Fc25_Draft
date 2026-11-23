namespace Fc25Draft.Core.Utilities;

/// <summary>Um jogo da fase de liga da Copa: rodada (1 a 4) e mando.</summary>
public sealed record JogoDaFaseDeLiga(int Rodada, Guid CasaId, Guid ForaId);

/// <summary>Resultado do sorteio: os potes (do mais forte para o mais fraco) e os jogos.</summary>
public sealed record SorteioDaFaseDeLiga(
    IReadOnlyList<Guid> Pote1,
    IReadOnlyList<Guid> Pote2,
    IReadOnlyList<JogoDaFaseDeLiga> Jogos)
{
    /// <summary>Adversários do time, na ordem das rodadas, com o mando dele.</summary>
    public IEnumerable<(int Rodada, Guid AdversarioId, bool EmCasa)> Adversarios(Guid timeId) =>
        Jogos.Where(j => j.CasaId == timeId || j.ForaId == timeId)
            .OrderBy(j => j.Rodada)
            .Select(j => (j.Rodada, j.CasaId == timeId ? j.ForaId : j.CasaId, j.CasaId == timeId));
}

/// <summary>
/// Sorteio da fase de liga da Copa (todos numa tabela só, estilo Champions). Os times vêm do mais
/// forte para o mais fraco e se dividem em dois potes iguais; cada um faz <see cref="JogosPorTime"/>
/// jogos — 2 contra times do próprio pote e 2 do outro, 2 em casa e 2 fora, um por rodada, sem
/// confronto repetido. Entre muitos sorteios válidos, fica um dos mais equilibrados: aquele em que a
/// força média dos adversários (a posição no ranking) é mais parecida de um time para o outro.
/// </summary>
public static class SorteioFaseDeLiga
{
    public const int JogosPorTime = 4;
    private const int DoMesmoPote = 2;

    /// <summary>Divide o ranking (do mais forte para o mais fraco) nos dois potes.</summary>
    public static (IReadOnlyList<Guid> Pote1, IReadOnlyList<Guid> Pote2) Potes(IReadOnlyList<Guid> ranking)
    {
        var metade = ranking.Count / 2;
        return (ranking.Take(metade).ToList(), ranking.Skip(metade).ToList());
    }

    /// <summary>
    /// Sorteia os jogos. <paramref name="ranking"/> vai do mais forte para o mais fraco e precisa de
    /// um número par de times, pelo menos 6 (3 por pote).
    /// </summary>
    public static SorteioDaFaseDeLiga Sortear(IReadOnlyList<Guid> ranking, Random? rng = null, int candidatos = 60)
    {
        rng ??= Random.Shared;
        if (ranking.Count < 6 || ranking.Count % 2 != 0)
            throw new InvalidOperationException("A fase de liga precisa de um número par de times, no mínimo 6.");
        if (ranking.Distinct().Count() != ranking.Count)
            throw new InvalidOperationException("Time repetido no ranking.");

        var (pote1, pote2) = Potes(ranking);
        var posicao = ranking.Select((id, i) => (id, i + 1)).ToDictionary(x => x.id, x => x.Item2);
        var doPote1 = pote1.ToHashSet();

        var validos = new List<(List<JogoDaFaseDeLiga> Jogos, double Diferenca)>();
        for (int i = 0; i < candidatos * 5 && validos.Count < candidatos; i++)
        {
            if (TentarRodadas(ranking, doPote1, posicao, rng) is not { } rodadas) continue;
            Equilibrar(rodadas, doPote1, posicao, rng);
            var jogos = DarMando(rodadas, rng);
            validos.Add((jogos, DiferencaDeForca(jogos, posicao)));
        }

        if (validos.Count == 0)
            throw new InvalidOperationException("Não foi possível montar os jogos da fase de liga. Tente de novo.");

        // Um dos mais equilibrados, ainda no sorteio: os 5% melhores concorrem entre si.
        var melhores = validos.OrderBy(v => v.Diferenca).Take(Math.Max(1, validos.Count / 20)).ToList();
        var escolhido = melhores[rng.Next(melhores.Count)].Jogos;

        return new SorteioDaFaseDeLiga(pote1, pote2, escolhido.OrderBy(j => j.Rodada).ToList());
    }

    /// <summary>
    /// Diferença entre o time com a tabela mais difícil e o com a mais fácil: média das posições no
    /// ranking dos adversários (quanto menor a média, mais fortes os adversários).
    /// </summary>
    public static double DiferencaDeForca(IEnumerable<JogoDaFaseDeLiga> jogos, IReadOnlyDictionary<Guid, int> posicao)
    {
        var medias = MediaDosAdversarios(jogos, posicao).Values.ToList();
        return medias.Count == 0 ? 0 : medias.Max() - medias.Min();
    }

    /// <summary>Média da posição no ranking dos adversários de cada time.</summary>
    public static Dictionary<Guid, double> MediaDosAdversarios(IEnumerable<JogoDaFaseDeLiga> jogos, IReadOnlyDictionary<Guid, int> posicao)
    {
        var soma = new Dictionary<Guid, (int Total, int Jogos)>();
        void Somar(Guid time, Guid adversario)
        {
            soma.TryGetValue(time, out var s);
            soma[time] = (s.Total + posicao[adversario], s.Jogos + 1);
        }

        foreach (var j in jogos)
        {
            Somar(j.CasaId, j.ForaId);
            Somar(j.ForaId, j.CasaId);
        }

        return soma.ToDictionary(x => x.Key, x => (double)x.Value.Total / x.Value.Jogos);
    }

    /// <summary>
    /// Monta as rodadas uma a uma, cada uma emparelhando todos os times ao acaso dentro das regras
    /// (sem repetir confronto, no máximo 2 jogos contra o próprio pote e 2 contra o outro). O acaso é
    /// puxado para o equilíbrio: tem mais chance o adversário que deixa a média dos dois times mais
    /// perto da média de todos os outros. Nulo se travar: quem chama tenta de novo.
    /// </summary>
    private static List<List<(Guid A, Guid B)>>? TentarRodadas(
        IReadOnlyList<Guid> times, HashSet<Guid> doPote1, IReadOnlyDictionary<Guid, int> posicao, Random rng)
    {
        var somaDasPosicoes = times.Sum(t => posicao[t]);
        double Alvo(Guid t) => (double)(somaDasPosicoes - posicao[t]) / (times.Count - 1);
        var somaAdversarios = times.ToDictionary(t => t, _ => 0);
        var jogados = times.ToDictionary(t => t, _ => 0);
        double Desvio(Guid t, Guid adversario) =>
            Math.Abs(somaAdversarios[t] + posicao[adversario] - (jogados[t] + 1) * Alvo(t));
        var ruido = times.Count / 3.0;

        var jaJogaram = new HashSet<(Guid, Guid)>();
        var mesmoPote = times.ToDictionary(t => t, _ => 0);
        var outroPote = times.ToDictionary(t => t, _ => 0);
        var rodadas = new List<List<(Guid, Guid)>>();

        for (int r = 0; r < JogosPorTime; r++)
        {
            List<(Guid, Guid)>? rodada = null;
            for (int tentativa = 0; tentativa < 30 && rodada is null; tentativa++)
            {
                var passos = 0;
                rodada = Emparelhar(times.OrderBy(_ => rng.Next()).ToList(), new List<(Guid, Guid)>(), ref passos);
            }
            if (rodada is null) return null;

            foreach (var (a, b) in rodada)
            {
                jaJogaram.Add(Par(a, b));
                somaAdversarios[a] += posicao[b]; somaAdversarios[b] += posicao[a];
                jogados[a]++; jogados[b]++;
                if (doPote1.Contains(a) == doPote1.Contains(b)) { mesmoPote[a]++; mesmoPote[b]++; }
                else { outroPote[a]++; outroPote[b]++; }
            }
            rodadas.Add(rodada);
        }

        return rodadas;

        List<(Guid, Guid)>? Emparelhar(List<Guid> livres, List<(Guid, Guid)> feitos, ref int passos)
        {
            if (livres.Count == 0) return feitos;
            if (++passos > 2000) return null;

            var time = livres[0];
            foreach (var outro in livres.Skip(1).OrderBy(o => Desvio(time, o) + Desvio(o, time) + rng.NextDouble() * ruido))
            {
                if (jaJogaram.Contains(Par(time, outro))) continue;
                var mesmo = doPote1.Contains(time) == doPote1.Contains(outro);
                if (mesmo ? mesmoPote[time] >= DoMesmoPote || mesmoPote[outro] >= DoMesmoPote
                          : outroPote[time] >= JogosPorTime - DoMesmoPote || outroPote[outro] >= JogosPorTime - DoMesmoPote)
                    continue;

                var resto = livres.Where(t => t != time && t != outro).ToList();
                feitos.Add((time, outro));
                if (Emparelhar(resto, feitos, ref passos) is { } pronto) return pronto;
                feitos.RemoveAt(feitos.Count - 1);
            }

            return null;
        }
    }

    /// <summary>
    /// Ajuste fino: em cada rodada, troca os adversários de dois jogos (A x B e C x D viram A x C e
    /// B x D, ou A x D e B x C) quando isso deixa as médias mais perto do alvo de cada time, sem
    /// quebrar as regras (confronto repetido ou a conta de jogos por pote). Para quando nenhuma troca ajuda.
    /// </summary>
    private static void Equilibrar(
        List<List<(Guid A, Guid B)>> rodadas, HashSet<Guid> doPote1, IReadOnlyDictionary<Guid, int> posicao, Random rng)
    {
        var times = posicao.Keys.ToList();
        var somaDasPosicoes = times.Sum(t => posicao[t]);
        var alvo = times.ToDictionary(t => t, t => JogosPorTime * (double)(somaDasPosicoes - posicao[t]) / (times.Count - 1));
        var soma = times.ToDictionary(t => t, _ => 0);
        var pares = new HashSet<(Guid, Guid)>();
        foreach (var (a, b) in rodadas.SelectMany(r => r))
        {
            soma[a] += posicao[b]; soma[b] += posicao[a];
            pares.Add(Par(a, b));
        }

        double Erro(Guid t, int novaSoma) => Math.Pow(novaSoma - alvo[t], 2);
        bool MesmoPote(Guid x, Guid y) => doPote1.Contains(x) == doPote1.Contains(y);

        for (int volta = 0; volta < 50; volta++)
        {
            var melhorou = false;
            foreach (var rodada in rodadas.OrderBy(_ => rng.Next()))
            {
                for (int i = 0; i < rodada.Count; i++)
                for (int j = i + 1; j < rodada.Count; j++)
                {
                    var (a, b) = rodada[i];
                    var (c, d) = rodada[j];
                    foreach (var (x1, y1, x2, y2) in new[] { (a, c, b, d), (a, d, b, c) })
                    {
                        // Cada time troca de adversário por outro do mesmo tipo de pote (abaixo).
                        var novos = new[] { (x1, y1), (x2, y2) };
                        if (novos.Any(n => pares.Contains(Par(n.Item1, n.Item2)))) continue;

                        var adversarioNovo = new Dictionary<Guid, Guid> { [x1] = y1, [y1] = x1, [x2] = y2, [y2] = x2 };
                        var adversarioVelho = new Dictionary<Guid, Guid> { [a] = b, [b] = a, [c] = d, [d] = c };
                        if (adversarioNovo.Any(kv => MesmoPote(kv.Key, kv.Value) != MesmoPote(kv.Key, adversarioVelho[kv.Key])))
                            continue;

                        var antes = adversarioNovo.Keys.Sum(t => Erro(t, soma[t]));
                        var depois = adversarioNovo.Keys.Sum(t => Erro(t, soma[t] - posicao[adversarioVelho[t]] + posicao[adversarioNovo[t]]));
                        if (depois >= antes - 1e-9) continue;

                        foreach (var t in adversarioNovo.Keys)
                            soma[t] += posicao[adversarioNovo[t]] - posicao[adversarioVelho[t]];
                        pares.Remove(Par(a, b)); pares.Remove(Par(c, d));
                        pares.Add(Par(x1, y1)); pares.Add(Par(x2, y2));
                        rodada[i] = (x1, y1);
                        rodada[j] = (x2, y2);
                        melhorou = true;
                        break;
                    }
                }
            }
            if (!melhorou) return;
        }
    }

    /// <summary>
    /// Mando: cada time joga 2 em casa e 2 fora. Como cada time tem 4 jogos, percorrer cada parte
    /// ligada da tabela num circuito (que entra e sai de cada time o mesmo número de vezes) dá
    /// exatamente isso. Entre algumas voltas sorteadas, fica a que menos repete mando em rodadas seguidas.
    /// </summary>
    private static List<JogoDaFaseDeLiga> DarMando(List<List<(Guid A, Guid B)>> rodadas, Random rng)
    {
        var arestas = rodadas
            .SelectMany((rodada, r) => rodada.Select(j => (Rodada: r + 1, j.A, j.B)))
            .ToList();

        List<JogoDaFaseDeLiga>? melhor = null;
        var menosRepeticoes = int.MaxValue;
        for (int volta = 0; volta < 20; volta++)
        {
            var jogos = CircuitoOrientado(arestas, rng);
            var repeticoes = Repeticoes(jogos);
            if (repeticoes < menosRepeticoes)
            {
                melhor = jogos;
                menosRepeticoes = repeticoes;
            }
        }

        return melhor!;
    }

    private static List<JogoDaFaseDeLiga> CircuitoOrientado(List<(int Rodada, Guid A, Guid B)> arestas, Random rng)
    {
        var usada = new bool[arestas.Count];
        var porTime = new Dictionary<Guid, List<int>>();
        for (int i = 0; i < arestas.Count; i++)
        {
            foreach (var t in new[] { arestas[i].A, arestas[i].B })
            {
                if (!porTime.TryGetValue(t, out var lista)) porTime[t] = lista = new List<int>();
                lista.Add(i);
            }
        }
        foreach (var lista in porTime.Values)
        {
            var embaralhada = lista.OrderBy(_ => rng.Next()).ToList();
            lista.Clear();
            lista.AddRange(embaralhada);
        }

        var jogos = new List<JogoDaFaseDeLiga>();
        foreach (var inicio in porTime.Keys.OrderBy(_ => rng.Next()))
        {
            // Hierholzer sem recursão: o sentido em que cada aresta é percorrida vira o mando.
            var pilha = new Stack<Guid>();
            pilha.Push(inicio);
            while (pilha.Count > 0)
            {
                var atual = pilha.Peek();
                var proxima = porTime[atual].FirstOrDefault(i => !usada[i], -1);
                if (proxima < 0) { pilha.Pop(); continue; }

                usada[proxima] = true;
                var (rodada, a, b) = arestas[proxima];
                var destino = a == atual ? b : a;
                jogos.Add(new JogoDaFaseDeLiga(rodada, atual, destino));
                pilha.Push(destino);
            }
        }

        return jogos;
    }

    /// <summary>Quantas vezes um time tem o mesmo mando em duas rodadas seguidas.</summary>
    private static int Repeticoes(List<JogoDaFaseDeLiga> jogos) =>
        jogos.SelectMany(j => new[] { (Time: j.CasaId, j.Rodada, Casa: true), (Time: j.ForaId, j.Rodada, Casa: false) })
            .GroupBy(x => x.Time)
            .Sum(g =>
            {
                var seq = g.OrderBy(x => x.Rodada).Select(x => x.Casa).ToList();
                return seq.Zip(seq.Skip(1), (x, y) => x == y ? 1 : 0).Sum();
            });

    private static (Guid, Guid) Par(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);
}
