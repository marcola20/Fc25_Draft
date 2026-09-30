using System.Text.Json;
using System.Text.Json.Serialization;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Overall do PES 2021 calculado pelos atributos, pela posição registrada no jogo e pelo pé fraco.
/// O jogo não grava o overall: calcula na hora. Esta é a mesma fórmula do Editor PES
/// (D:\PES\EditorPES\editor\overall.py, arquivo Data/formula-overall-pes.json), ajustada com jogadores do
/// pesdb.net e conferida no jogo: bate exato em ~95% dos jogadores da liga e o resto fica a 1 ponto.
///
/// A conta, por posição:
///   1. s = soma(w1 · [25 atributos, pé fraco]) — uma "nota base" do jogador;
///   2. overall = soma(w2 · [25 atributos, pé fraco, degraus de s]) — cada degrau (50, 55, … 95) que s passa
///      muda o peso dos atributos; é isso que faz jogador fraco e craque ficarem abaixo de uma conta linear.
/// </summary>
public static class OverallPes
{
    public const int NumAtributos = 25;
    public const double Tolerancia = 0.1;

    /// <summary>Posições registradas no PES, na ordem gravada no jogo (0 = GK … 12 = CF).</summary>
    public static readonly IReadOnlyList<string> Posicoes =
        ["GK", "CB", "LB", "RB", "DMF", "CMF", "LMF", "RMF", "AMF", "LWF", "RWF", "SS", "CF"];

    public static readonly IReadOnlyList<string> PosicoesPt =
        ["GOL", "ZAG", "LTE", "LTD", "VOL", "MLG", "MLE", "MLD", "MAT", "PTE", "PTD", "SA", "CA"];

    public static readonly IReadOnlyList<string> PosicoesNome =
    [
        "Goleiro", "Zagueiro", "Lateral Esquerdo", "Lateral Direito", "Volante", "Meia de Ligação", "Meia Esquerda",
        "Meia Direita", "Meia Atacante", "Ponta Esquerda", "Ponta Direita", "Segundo Atacante", "Centroavante",
    ];

    /// <summary>Siglas dos atributos, na ordem de <see cref="AtributosPes.Todos"/> (a mesma da fórmula).</summary>
    private static readonly string[] Siglas =
    [
        "OA", "BC", "DRI", "TIG", "LP", "LOF", "FIN", "HEA", "PK", "CUR",
        "SPD", "ACC", "KP", "JMP", "PHY", "BAL", "STA",
        "DA", "BW", "AGG",
        "GKA", "GKC", "GKCL", "GKR", "GKRE",
    ];

    /// <summary>Atributos que cada estilo de jogo pede (código = índice em HabilidadesPes.EstilosDeJogo).
    /// Na evolução, sobem primeiro.</summary>
    private static readonly Dictionary<int, string[]> AtributosDoEstilo = new()
    {
        [1] = ["OA", "FIN", "ACC", "SPD"], [2] = ["OA", "ACC", "SPD", "BC"], [3] = ["FIN", "OA", "HEA", "PHY"],
        [4] = ["HEA", "PHY", "JMP", "BC"], [5] = ["DRI", "LP", "BC", "TIG"], [6] = ["SPD", "ACC", "DRI", "FIN"],
        [7] = ["SPD", "DRI", "ACC", "TIG"], [8] = ["LOF", "CUR", "SPD", "STA"], [9] = ["BC", "DRI", "LP", "TIG"],
        [10] = ["OA", "FIN", "BC", "DRI"], [11] = ["STA", "BW", "LP", "DA"], [12] = ["DA", "BW", "PHY", "HEA"],
        [13] = ["BW", "AGG", "DA", "STA"], [14] = ["LP", "LOF", "BC", "TIG"], [15] = ["SPD", "STA", "LOF", "ACC"],
        [16] = ["DA", "BW", "STA", "SPD"], [17] = ["SPD", "STA", "FIN", "OA"], [18] = ["DA", "BW", "HEA", "OA"],
        [19] = ["LP", "LOF", "DA", "BC"], [20] = ["GKA", "GKR", "GKRE", "GKC"], [21] = ["GKA", "GKR", "GKRE", "GKC"],
    };

    /// <summary>Atributos com peso acima disso entram na evolução.</summary>
    private const double PesoRelevante = 0.05;

    private sealed record Modelo(
        [property: JsonPropertyName("w1")] double[] W1,
        [property: JsonPropertyName("w2")] double[] W2);

    private sealed record Arquivo(
        [property: JsonPropertyName("nos")] double[] Nos,
        [property: JsonPropertyName("pe_padrao")] int[] PePadrao,
        [property: JsonPropertyName("posicoes")] Dictionary<string, Modelo> Posicoes);

    private static readonly Lazy<Arquivo> Formula = new(() =>
    {
        using var recurso = typeof(OverallPes).Assembly.GetManifestResourceStream("formula-overall-pes.json")
                            ?? throw new InvalidOperationException("Fórmula do overall (formula-overall-pes.json) não encontrada.");
        return JsonSerializer.Deserialize<Arquivo>(recurso)
               ?? throw new InvalidOperationException("Fórmula do overall vazia.");
    });

    /// <summary>Degraus da curva (valores de s em que o peso dos atributos muda).</summary>
    public static IReadOnlyList<double> Degraus => Formula.Value.Nos;

    /// <summary>Arredonda como o jogo: 85,5 vira 86.</summary>
    public static int Arredondar(double x) => (int)Math.Floor(x + 0.5);

    public static int[] Valores(PlayerAtributosDto a) => AtributosPes.Todos.Select(x => x.Get(a)).ToArray();

    private static double[] PeFraco(int? uso, int? precisao)
    {
        var u = uso ?? Formula.Value.PePadrao[0];
        var p = precisao ?? Formula.Value.PePadrao[1];
        return [u == 2 ? 1 : 0, u == 3 ? 1 : 0, u == 4 ? 1 : 0, p == 2 ? 1 : 0, p == 3 ? 1 : 0, p == 4 ? 1 : 0];
    }

    private static (Modelo m, double[] x, double s) Soma(IReadOnlyList<int> valores, int pos, int? peUso, int? pePrecisao)
    {
        var m = Formula.Value.Posicoes[Posicoes[pos]];
        var x = valores.Select(v => (double)v).Concat(PeFraco(peUso, pePrecisao)).ToArray();
        var s = m.W1[^1];
        for (var i = 0; i < x.Length; i++) s += m.W1[i] * x[i];
        return (m, x, s);
    }

    /// <summary>Overall com fração (o jogo mostra arredondado).</summary>
    public static double Calcular(IReadOnlyList<int> valores, int pos, int? peUso = null, int? pePrecisao = null)
    {
        var (m, x, s) = Soma(valores, pos, peUso, pePrecisao);
        var total = m.W2[^1];
        for (var i = 0; i < x.Length; i++) total += m.W2[i] * x[i];
        var nos = Formula.Value.Nos;
        for (var k = 0; k < nos.Length; k++) total += m.W2[x.Length + k] * Math.Max(0, s - nos[k]);
        return total;
    }

    /// <summary>Quanto +1 ponto em cada atributo mexe no overall deste jogador (depende de onde ele está na curva).
    /// Sem valores, usa um jogador de nível 70.</summary>
    public static double[] Pesos(int pos, IReadOnlyList<int>? valores = null, int? peUso = null, int? pePrecisao = null)
    {
        var m = Formula.Value.Posicoes[Posicoes[pos]];
        var s = valores is null ? 70.0 : Soma(valores, pos, peUso, pePrecisao).s;
        return PesosNaCurva(m, s);
    }

    /// <summary>Pesos para um jogador cuja nota base (s) é <paramref name="nivel"/>.</summary>
    public static double[] PesosNoNivel(int pos, double nivel) =>
        PesosNaCurva(Formula.Value.Posicoes[Posicoes[pos]], nivel);

    private static double[] PesosNaCurva(Modelo m, double s)
    {
        var n = m.W1.Length - 1; // atributos + pé fraco
        var nos = Formula.Value.Nos;
        var curva = 0.0;
        for (var k = 0; k < nos.Length; k++)
            if (s > nos[k]) curva += m.W2[n + k];
        var pesos = new double[NumAtributos];
        for (var i = 0; i < NumAtributos; i++) pesos[i] = m.W2[i] + curva * m.W1[i];
        return pesos;
    }

    /// <summary>
    /// Overall do jogador pelos atributos, ou null se não dá para confiar neles. Só vale para quem tem a posição
    /// registrada no PES: ela vem junto com atributos tirados do jogo (Editor PES) ou da base atual do PES.
    /// Os atributos antigos (migration de 28/09, antes dos ajustes no editor) não têm e continuam com o overall
    /// digitado até o editor mandar os do jogo.
    /// </summary>
    public static double? CalcularDoJogador(PlayerAtributosDto? a) =>
        a?.PosicaoPes is int pos and >= 0 and < 13 ? Calcular(Valores(a), pos, a.PeFracoUso, a.PeFracoPrecisao) : null;

    /// <summary>Acerta <c>Player.Overall</c> pela fórmula quando o jogador tem atributos do jogo (precisa de
    /// <c>Atributos</c> carregado). Devolve true se o overall mudou.</summary>
    public static bool Recalcular(Player jogador)
    {
        if (jogador.Atributos is null) return false;
        var calculado = CalcularDoJogador(AtributosPes.ParaDto(jogador.Atributos));
        if (calculado is not double valor) return false;
        var novo = Math.Clamp(Arredondar(valor), 1, 99);
        if (novo == jogador.Overall) return false;
        jogador.Overall = novo;
        return true;
    }

    public record Evolucao(int[] Novos, double Estimado, bool Alcancou);

    /// <summary>
    /// Sobe atributos até a fórmula dar <paramref name="alvo"/> (mirando o centro: alvo ± 0,1), como o "Ajustar"
    /// do Editor PES: primeiro os atributos do estilo de jogo, depois os de maior peso na posição, 1 ponto por
    /// vez em rodízio (goleiro: só atributos de goleiro). No fim, troca +1/−1 para cair no centro.
    /// </summary>
    public static Evolucao Evoluir(IReadOnlyList<int> atual, int pos, int? estilo, int? peUso, int? pePrecisao, int alvo)
    {
        var novo = atual.ToArray();
        double Estimado(int[] v) => Calcular(v, pos, peUso, pePrecisao);

        if (Estimado(novo) >= alvo - Tolerancia)
            return new Evolucao(novo, Estimado(novo), true);

        var w = Pesos(pos, atual, peUso, pePrecisao);
        var prio = OrdemDePrioridade(pos, estilo, w);

        // Rodízio: +1 em cada um da lista até chegar no alvo ou todos baterem 99.
        var alcancou = true;
        while (Estimado(novo) < alvo - Tolerancia)
        {
            var mexeu = false;
            foreach (var i in prio)
            {
                if (Estimado(novo) >= alvo - Tolerancia) break;
                if (novo[i] >= AtributosPes.Maximo) continue;
                novo[i]++;
                mexeu = true;
            }
            if (!mexeu) { alcancou = false; break; }
        }

        if (alcancou)
        {
            // Equilibrar: +1 em quem é da lista, −1 em quem subiu agora; aceita a troca que mais aproxima do centro.
            var permitidos = Permitidos(pos);
            var desce = Enumerable.Range(0, NumAtributos)
                .Where(i => permitidos(i) && w[i] > 0 && (prio.Contains(i) || novo[i] != atual[i])).ToList();
            for (var passo = 0; passo < 40; passo++)
            {
                var erro = Math.Abs(Estimado(novo) - alvo);
                if (erro <= Tolerancia) break;
                var ups = prio.Where(i => novo[i] < AtributosPes.Maximo).ToList();
                var downs = desce.Where(i => novo[i] > atual[i]).ToList();
                var melhor = erro;
                (int i, int d)[]? troca = null;

                double ErroCom(params (int i, int d)[] mudancas)
                {
                    var teste = novo.ToArray();
                    foreach (var (i, d) in mudancas) teste[i] += d;
                    return Math.Abs(Estimado(teste) - alvo);
                }

                foreach (var i in ups)
                {
                    var e = ErroCom((i, +1));
                    if (e < melhor - 1e-9) { melhor = e; troca = [(i, +1)]; }
                }
                foreach (var j in downs)
                {
                    var e = ErroCom((j, -1));
                    if (e < melhor - 1e-9) { melhor = e; troca = [(j, -1)]; }
                }
                foreach (var i in ups)
                foreach (var j in downs)
                {
                    if (i == j) continue;
                    var e = ErroCom((i, +1), (j, -1));
                    if (e < melhor - 1e-9) { melhor = e; troca = [(i, +1), (j, -1)]; }
                }
                if (troca is null) break;
                foreach (var (i, d) in troca) novo[i] += d;
            }
        }

        return new Evolucao(novo, Estimado(novo), alcancou);
    }

    private static Func<int, bool> Permitidos(int pos) => pos == 0 ? i => i >= 20 : i => i < 20;

    /// <summary>Estilo de jogo primeiro, depois os maiores pesos da posição.</summary>
    private static List<int> OrdemDePrioridade(int pos, int? estilo, double[] w)
    {
        var permitidos = Permitidos(pos);
        var doEstilo = estilo is int e && AtributosDoEstilo.TryGetValue(e, out var siglas)
            ? siglas.Select(s => Array.IndexOf(Siglas, s))
            : Enumerable.Empty<int>();
        var porPeso = Enumerable.Range(0, NumAtributos).OrderBy(i => -w[i]).Where(i => w[i] > PesoRelevante);
        return doEstilo.Concat(porPeso).Distinct().Where(permitidos).ToList();
    }

    /// <summary>"Finalização +2, Velocidade +1" (só quem mudou).</summary>
    public static string DescreverMudancas(IReadOnlyList<int> antes, IReadOnlyList<int> depois) =>
        string.Join(", ", Enumerable.Range(0, NumAtributos)
            .Where(i => depois[i] != antes[i])
            .Select(i => $"{AtributosPes.Todos[i].Nome} {depois[i] - antes[i]:+#;-#}"));
}
