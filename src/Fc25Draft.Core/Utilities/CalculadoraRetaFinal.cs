namespace Fc25Draft.Core.Utilities;

public enum SituacaoObjetivo
{
    /// <summary>Já garantiu, aconteça o que acontecer.</summary>
    Garantido,
    /// <summary>Garante sozinho se fizer os pontos indicados.</summary>
    DependeDeSi,
    /// <summary>Ainda dá, mas precisa de tropeço de outros.</summary>
    PrecisaDeAjuda,
    /// <summary>Não alcança mais nem vencendo tudo.</summary>
    Impossivel
}

/// <summary>Um time como está na tabela hoje.</summary>
public sealed record RetaFinalTime(Guid TimeId, string Nome, int Pontos, int Jogos, int Vitorias, int Saldo);

/// <summary>Um jogo que ainda falta.</summary>
public sealed record RetaFinalJogo(Guid CasaId, Guid ForaId);

/// <summary>
/// Objetivo de tabela: terminar entre os <paramref name="Ate"/> primeiros. Com <paramref name="EmpateEmPontosVaiAJogo"/>,
/// quem empata em pontos na fronteira não se separa por vitórias nem saldo: a vaga sai em jogo decisivo (2 times) ou
/// mini liga (3+), e no sorteio cada empatado fica com a mesma fração dela (título da Série A).
/// </summary>
public sealed record RetaFinalObjetivo(string Nome, int Ate, bool EmpateEmPontosVaiAJogo = false);

public sealed record RetaFinalMeta(SituacaoObjetivo Situacao, int? PontosParaGarantir, double Chance);

public sealed record RetaFinalLinha(RetaFinalTime Time, int Posicao, int JogosRestantes, int PontosMaximos, IReadOnlyList<RetaFinalMeta> Metas);

public sealed record RetaFinalResultado(IReadOnlyList<RetaFinalObjetivo> Objetivos, IReadOnlyList<RetaFinalLinha> Linhas, int JogosRestantes, int Simulacoes);

/// <summary>
/// Calculadora da reta final de uma liga de pontos corridos.
/// <para>
/// <b>Contas exatas</b> (só pontos): com até <see cref="MaxJogosNaContaExata"/> jogos faltando, testa todas as
/// combinações de resultado, então leva em conta os confrontos entre os rivais (Grêmio x Flamengo não deixa os dois
/// vencerem). Com mais jogos, usa a conta simples pelo máximo de cada um: garantido quando no máximo <c>Ate − 1</c> rivais ainda alcançam a pontuação
/// atual do time; impossível quando <c>Ate</c> rivais já têm mais pontos do que o máximo dele. "Garante com X pontos"
/// é passar o máximo do <c>Ate</c>º rival: vale mesmo com os outros vencendo tudo (empate em pontos não conta como
/// garantido, porque vira desempate ou jogo decisivo).
/// </para>
/// <para>
/// <b>Chances</b>: sorteia os jogos que faltam milhares de vezes. Cada jogo pende para quem tem a melhor campanha
/// (aproveitamento); empate em pontos é desfeito por vitórias, saldo e, por fim, sorteio — menos no objetivo
/// marcado com <see cref="RetaFinalObjetivo.EmpateEmPontosVaiAJogo"/> (título da Série A), em que os empatados
/// dividem a vaga, como num jogo decisivo / mini liga.
/// </para>
/// </summary>
public static class CalculadoraRetaFinal
{
    public const int SimulacoesPadrao = 10_000;

    /// <summary>Os objetivos que fazem sentido na liga: título e as zonas de acesso ou de rebaixamento.</summary>
    public static IReadOnlyList<RetaFinalObjetivo> Objetivos(LigaRegraZonas regra, int totalTimes)
    {
        // Série A: empate em pontos no topo decide o título em jogo decisivo / mini liga. Na Série B o título segue o
        // desempate da tabela (vitórias, saldo, confronto direto), que o sorteio já aproxima.
        var objetivos = new List<RetaFinalObjetivo> { new("Título", 1, EmpateEmPontosVaiAJogo: !regra.DesempateSerieB) };
        if (!regra.TemAcessoRebaixamento) return objetivos;

        if (regra.DesempateSerieB)
        {
            if (regra.VagasDiretas > 1) objetivos.Add(new("Acesso direto", regra.VagasDiretas));
            if (regra.VagasPlayoff > 0) objetivos.Add(new("Ao menos o playoff", regra.VagasDiretas + regra.VagasPlayoff));
        }
        else
        {
            var foraDaQueda = totalTimes - regra.VagasDiretas;
            if (regra.VagasPlayoff > 0) objetivos.Add(new("Fugir do playoff", foraDaQueda - regra.VagasPlayoff));
            if (regra.VagasDiretas > 0) objetivos.Add(new("Fugir da queda direta", foraDaQueda));
        }

        return objetivos.Where(o => o.Ate >= 1 && o.Ate < totalTimes).ToList();
    }

    /// <param name="tabela">Os times na ordem da classificação atual.</param>
    public static RetaFinalResultado Calcular(
        IReadOnlyList<RetaFinalTime> tabela,
        IReadOnlyList<RetaFinalJogo> restantes,
        IReadOnlyList<RetaFinalObjetivo> objetivos,
        int simulacoes = SimulacoesPadrao,
        int semente = 2010)
    {
        var indice = tabela.Select((t, i) => (t.TimeId, i)).ToDictionary(x => x.TimeId, x => x.i);
        var jogos = restantes
            .Where(j => indice.ContainsKey(j.CasaId) && indice.ContainsKey(j.ForaId))
            .Select(j => (Casa: indice[j.CasaId], Fora: indice[j.ForaId]))
            .ToArray();

        var faltam = new int[tabela.Count];
        foreach (var (casa, fora) in jogos) { faltam[casa]++; faltam[fora]++; }
        var maximo = tabela.Select((t, i) => t.Pontos + 3 * faltam[i]).ToArray();

        var chances = Simular(tabela, jogos, objetivos, simulacoes, semente);
        var exato = jogos.Length <= MaxJogosNaContaExata ? Enumerar(tabela, jogos, faltam) : null;

        var linhas = tabela.Select((t, i) => new RetaFinalLinha(
                t, i + 1, faltam[i], maximo[i],
                objetivos.Select((o, k) => exato is null
                    ? Meta(tabela, maximo, faltam, i, o.Ate, chances[i, k])
                    : MetaExata(exato, tabela[i].Pontos, i, o.Ate, chances[i, k])).ToList()))
            .ToList();

        return new RetaFinalResultado(objetivos, linhas, jogos.Length, jogos.Length == 0 ? 0 : simulacoes);
    }

    /// <summary>
    /// Até aqui dá para testar todas as combinações de resultado (3^12 ≈ 530 mil), levando em conta quem joga
    /// contra quem: dois rivais que se enfrentam não podem vencer os dois.
    /// </summary>
    private const int MaxJogosNaContaExata = 12;

    /// <summary>
    /// Pior e melhor cenário de cada time em todas as combinações dos jogos que faltam.
    /// <c>PiorComPontos[i][p]</c>: o máximo de rivais com pontos ≥ os dele quando ele soma <c>p</c> pontos nos jogos
    /// que faltam (−1 = ele não consegue somar exatamente <c>p</c>). <c>MelhorAcima[i]</c>: o mínimo de rivais com
    /// mais pontos do que ele, em qualquer cenário.
    /// </summary>
    private sealed record ContaExata(int[][] PiorComPontos, int[] MelhorAcima);

    private static ContaExata Enumerar(IReadOnlyList<RetaFinalTime> tabela, (int Casa, int Fora)[] jogos, int[] faltam)
    {
        var n = tabela.Count;
        var pior = Enumerable.Range(0, n).Select(i => Enumerable.Repeat(-1, 3 * faltam[i] + 1).ToArray()).ToArray();
        var melhorAcima = Enumerable.Repeat(int.MaxValue, n).ToArray();
        var pontos = new int[n];
        var resultado = new int[jogos.Length]; // 0 = mandante vence, 1 = empate, 2 = visitante vence

        while (true)
        {
            for (var t = 0; t < n; t++) pontos[t] = tabela[t].Pontos;
            for (var j = 0; j < jogos.Length; j++)
            {
                var (casa, fora) = jogos[j];
                switch (resultado[j])
                {
                    case 0: pontos[casa] += 3; break;
                    case 1: pontos[casa]++; pontos[fora]++; break;
                    default: pontos[fora] += 3; break;
                }
            }

            for (var i = 0; i < n; i++)
            {
                int maisOuIgual = 0, mais = 0;
                for (var o = 0; o < n; o++)
                {
                    if (o == i) continue;
                    if (pontos[o] >= pontos[i]) maisOuIgual++;
                    if (pontos[o] > pontos[i]) mais++;
                }

                var somou = pontos[i] - tabela[i].Pontos;
                if (maisOuIgual > pior[i][somou]) pior[i][somou] = maisOuIgual;
                if (mais < melhorAcima[i]) melhorAcima[i] = mais;
            }

            // Próxima combinação (contador em base 3).
            var k = 0;
            while (k < resultado.Length && ++resultado[k] == 3) resultado[k++] = 0;
            if (k == resultado.Length) break;
        }

        return new ContaExata(pior, melhorAcima);
    }

    private static RetaFinalMeta MetaExata(ContaExata conta, int pontos, int i, int ate, double chance)
    {
        var pior = conta.PiorComPontos[i];

        // Mesmo perdendo tudo e com os rivais somando o máximo possível entre si, no máximo ate − 1 chegam aos pontos dele.
        if (pior.All(p => p <= ate - 1))
            return new RetaFinalMeta(SituacaoObjetivo.Garantido, null, 1);
        if (conta.MelhorAcima[i] >= ate)
            return new RetaFinalMeta(SituacaoObjetivo.Impossivel, null, 0);

        // Menor pontuação que garante: dali para cima, todo cenário possível deixa no máximo ate − 1 rivais empatados ou à frente.
        int? precisa = null;
        for (var p = pior.Length - 1; p >= 0; p--)
        {
            if (pior[p] < 0) continue;
            if (pior[p] > ate - 1) break;
            precisa = p;
        }

        return precisa is int x
            ? new RetaFinalMeta(SituacaoObjetivo.DependeDeSi, x, chance)
            : new RetaFinalMeta(SituacaoObjetivo.PrecisaDeAjuda, null, chance);
    }

    private static RetaFinalMeta Meta(IReadOnlyList<RetaFinalTime> tabela, int[] maximo, int[] faltam, int i, int ate, double chance)
    {
        var pontos = tabela[i].Pontos;
        var rivais = Enumerable.Range(0, tabela.Count).Where(o => o != i).ToList();

        if (rivais.Count(o => maximo[o] >= pontos) <= ate - 1)
            return new RetaFinalMeta(SituacaoObjetivo.Garantido, null, 1);
        if (rivais.Count(o => tabela[o].Pontos > maximo[i]) >= ate)
            return new RetaFinalMeta(SituacaoObjetivo.Impossivel, null, 0);

        // Passar o máximo do ate-ésimo rival garante, mesmo que todos os outros vençam tudo.
        var alvo = rivais.Select(o => maximo[o]).OrderByDescending(m => m).ElementAt(ate - 1);
        var precisa = alvo - pontos + 1;
        return precisa <= 3 * faltam[i]
            ? new RetaFinalMeta(SituacaoObjetivo.DependeDeSi, precisa, chance)
            : new RetaFinalMeta(SituacaoObjetivo.PrecisaDeAjuda, null, chance);
    }

    /// <summary>
    /// Chance de vitória do mandante, de empate e de vitória do visitante, pela campanha dos dois: o aproveitamento
    /// de cada um, puxado para o meio como se todo time tivesse mais 8 jogos de campanha média (no começo da
    /// temporada um jogo só não pode decidir quem é forte). Times iguais: 37% · 26% · 37%.
    /// </summary>
    public static (double Casa, double Empate, double Fora) Chances(int pontosCasa, int jogosCasa, int pontosFora, int jogosFora)
    {
        var d = Forca(pontosCasa, jogosCasa) - Forca(pontosFora, jogosFora);
        var casa = Math.Clamp(0.37 + 0.30 * d, 0.08, 0.84);
        var fora = Math.Clamp(0.37 - 0.30 * d, 0.08, 0.84);
        return (casa, 1 - casa - fora, fora);
    }

    private static double Forca(int pontos, int jogos)
    {
        const double JogosDeReferencia = 8;
        return Math.Clamp((pontos + 1.5 * JogosDeReferencia) / (3.0 * (jogos + JogosDeReferencia)), 0, 1);
    }

    /// <summary>Fração das simulações em que cada time (linha) cumpriu cada objetivo (coluna).</summary>
    private static double[,] Simular(
        IReadOnlyList<RetaFinalTime> tabela, (int Casa, int Fora)[] jogos, IReadOnlyList<RetaFinalObjetivo> objetivos,
        int simulacoes, int semente)
    {
        var n = tabela.Count;
        var chances = new double[n, objetivos.Count];
        if (n == 0) return chances;

        // Sem jogos restantes, a tabela de hoje é a final.
        if (jogos.Length == 0) simulacoes = 1;

        var probabilidades = jogos.Select(j =>
        {
            var chances = Chances(tabela[j.Casa].Pontos, tabela[j.Casa].Jogos, tabela[j.Fora].Pontos, tabela[j.Fora].Jogos);
            return (chances.Casa, chances.Empate);
        }).ToArray();

        var sorteio = new Random(semente);
        var pontos = new int[n];
        var vitorias = new int[n];
        var saldo = new int[n];
        var desempate = new double[n];
        var ordem = new int[n];
        var contagem = new double[n, objetivos.Count];

        for (var s = 0; s < simulacoes; s++)
        {
            for (var t = 0; t < n; t++)
            {
                pontos[t] = tabela[t].Pontos;
                vitorias[t] = tabela[t].Vitorias;
                saldo[t] = tabela[t].Saldo;
                desempate[t] = sorteio.NextDouble();
                ordem[t] = t;
            }

            for (var j = 0; j < jogos.Length; j++)
            {
                var (casa, fora) = jogos[j];
                var r = sorteio.NextDouble();
                if (r < probabilidades[j].Casa) Vitoria(casa, fora);
                else if (r < probabilidades[j].Casa + probabilidades[j].Empate) { pontos[casa]++; pontos[fora]++; }
                else Vitoria(fora, casa);
            }

            Array.Sort(ordem, (a, b) =>
                pontos[a] != pontos[b] ? pontos[b].CompareTo(pontos[a])
                : vitorias[a] != vitorias[b] ? vitorias[b].CompareTo(vitorias[a])
                : saldo[a] != saldo[b] ? saldo[b].CompareTo(saldo[a])
                : desempate[b].CompareTo(desempate[a]));

            for (var k = 0; k < objetivos.Count; k++)
            {
                var ate = Math.Min(objetivos[k].Ate, n);
                for (var posicao = 0; posicao < ate; posicao++) contagem[ordem[posicao], k]++;

                if (!objetivos[k].EmpateEmPontosVaiAJogo) continue;

                // Bloco empatado em pontos que cruza a fronteira: as vagas que sobram nele são divididas por igual.
                var inicio = ate - 1;
                while (inicio > 0 && pontos[ordem[inicio - 1]] == pontos[ordem[ate - 1]]) inicio--;
                var fim = ate;
                while (fim < n && pontos[ordem[fim]] == pontos[ordem[ate - 1]]) fim++;
                if (fim == ate) continue;

                var fracao = (double)(ate - inicio) / (fim - inicio);
                for (var posicao = inicio; posicao < fim; posicao++)
                    contagem[ordem[posicao], k] += fracao - (posicao < ate ? 1 : 0);
            }
        }

        for (var t = 0; t < n; t++)
            for (var k = 0; k < objetivos.Count; k++)
                chances[t, k] = contagem[t, k] / simulacoes;
        return chances;

        void Vitoria(int vencedor, int perdedor)
        {
            pontos[vencedor] += 3;
            vitorias[vencedor]++;
            saldo[vencedor]++;
            saldo[perdedor]--;
        }
    }
}
