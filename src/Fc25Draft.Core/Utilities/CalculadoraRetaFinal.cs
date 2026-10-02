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

/// <summary>Objetivo de tabela: terminar entre os <paramref name="Ate"/> primeiros.</summary>
public sealed record RetaFinalObjetivo(string Nome, int Ate);

public sealed record RetaFinalMeta(SituacaoObjetivo Situacao, int? PontosParaGarantir, double Chance);

public sealed record RetaFinalLinha(RetaFinalTime Time, int Posicao, int JogosRestantes, int PontosMaximos, IReadOnlyList<RetaFinalMeta> Metas);

public sealed record RetaFinalResultado(IReadOnlyList<RetaFinalObjetivo> Objetivos, IReadOnlyList<RetaFinalLinha> Linhas, int JogosRestantes, int Simulacoes);

/// <summary>
/// Calculadora da reta final de uma liga de pontos corridos.
/// <para>
/// <b>Contas exatas</b> (só pontos): garantido quando no máximo <c>Ate − 1</c> rivais ainda alcançam a pontuação
/// atual do time; impossível quando <c>Ate</c> rivais já têm mais pontos do que o máximo dele. "Garante com X pontos"
/// é passar o máximo do <c>Ate</c>º rival: vale mesmo com os outros vencendo tudo (empate em pontos não conta como
/// garantido, porque vira desempate ou jogo decisivo).
/// </para>
/// <para>
/// <b>Chances</b>: sorteia os jogos que faltam milhares de vezes. Cada jogo pende para quem tem a melhor campanha
/// (aproveitamento); empate em pontos é desfeito por vitórias, saldo e, por fim, sorteio.
/// </para>
/// </summary>
public static class CalculadoraRetaFinal
{
    public const int SimulacoesPadrao = 10_000;

    /// <summary>Os objetivos que fazem sentido na liga: título e as zonas de acesso ou de rebaixamento.</summary>
    public static IReadOnlyList<RetaFinalObjetivo> Objetivos(LigaRegraZonas regra, int totalTimes)
    {
        var objetivos = new List<RetaFinalObjetivo> { new("Título", 1) };
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

        var linhas = tabela.Select((t, i) => new RetaFinalLinha(
                t, i + 1, faltam[i], maximo[i],
                objetivos.Select((o, k) => Meta(tabela, maximo, faltam, i, o.Ate, chances[i, k])).ToList()))
            .ToList();

        return new RetaFinalResultado(objetivos, linhas, jogos.Length, jogos.Length == 0 ? 0 : simulacoes);
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
        var contagem = new int[n, objetivos.Count];

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

            for (var posicao = 0; posicao < n; posicao++)
                for (var k = 0; k < objetivos.Count; k++)
                    if (posicao < objetivos[k].Ate) contagem[ordem[posicao], k]++;
        }

        for (var t = 0; t < n; t++)
            for (var k = 0; k < objetivos.Count; k++)
                chances[t, k] = (double)contagem[t, k] / simulacoes;
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
