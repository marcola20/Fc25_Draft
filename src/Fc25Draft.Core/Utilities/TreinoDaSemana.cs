namespace Fc25Draft.Core.Utilities;

/// <summary>Em que o jogador treina na semana.</summary>
public enum FocoDoTreino : short
{
    Finalizacao = 1,
    Passe = 2,
    Defesa = 3,
    Fisico = 4,
    Goleiro = 5
}

/// <summary>
/// Treino da semana: o time escolhe até 3 jovens do elenco e o foco de cada um; quando a semana fecha (domingo à
/// noite, Brasília), cada um ganha +1 em um atributo do foco, que vai para o Editor PES como a venda rápida.
/// </summary>
public static class TreinoCriterios
{
    public const int JogadoresPorSemana = 3;

    /// <summary>Treina quem tem até essa idade...</summary>
    public const int IdadeMaxima = 25;

    /// <summary>... e até esse overall. Passou disso, se formou: sai do treino.</summary>
    public const int OverallMaximo = 85;

    /// <summary>Quanto o atributo sobe por semana de treino.</summary>
    public const int GanhoPorSemana = 1;

    /// <summary>A partir desse dia da semana (sexta ao meio-dia), quem não montou o treino recebe o lembrete.</summary>
    public const DayOfWeek DiaDoLembrete = DayOfWeek.Friday;
    public const int HoraDoLembrete = 12;

    /// <summary>Os atributos de cada foco, pelos nomes de <see cref="AtributosPes.Todos"/>.</summary>
    public static readonly IReadOnlyDictionary<FocoDoTreino, IReadOnlyList<string>> Atributos =
        new Dictionary<FocoDoTreino, IReadOnlyList<string>>
        {
            [FocoDoTreino.Finalizacao] = ["Finalização", "Força do Chute", "Cabeceio", "Talento Ofensivo"],
            [FocoDoTreino.Passe] = ["Passe Rasteiro", "Passe Alto", "Controle de Bola", "Drible", "Condução Firme", "Curva", "Bola Parada"],
            [FocoDoTreino.Defesa] = ["Talento Defensivo", "Desarme", "Agressividade"],
            [FocoDoTreino.Fisico] = ["Velocidade", "Aceleração", "Impulsão", "Contato Físico", "Equilíbrio", "Resistência"],
            [FocoDoTreino.Goleiro] = ["Talento de Goleiro", "Firmeza", "Afastamento", "Reflexos", "Alcance"],
        };
}

public static class TreinoDaSemana
{
    public static string Emoji(FocoDoTreino foco) => foco switch
    {
        FocoDoTreino.Finalizacao => "⚽",
        FocoDoTreino.Passe => "🎯",
        FocoDoTreino.Defesa => "🛡️",
        FocoDoTreino.Fisico => "💨",
        _ => "🧤"
    };

    public static string Nome(FocoDoTreino foco) => foco switch
    {
        FocoDoTreino.Finalizacao => "Finalização",
        FocoDoTreino.Passe => "Passe",
        FocoDoTreino.Defesa => "Defesa",
        FocoDoTreino.Fisico => "Físico",
        _ => "Goleiro"
    };

    /// <summary>Os focos que o jogador pode treinar: goleiro treina goleiro e físico; os outros, o resto.</summary>
    public static IReadOnlyList<FocoDoTreino> Focos(bool goleiro) => goleiro
        ? [FocoDoTreino.Goleiro, FocoDoTreino.Fisico]
        : [FocoDoTreino.Finalizacao, FocoDoTreino.Passe, FocoDoTreino.Defesa, FocoDoTreino.Fisico];

    /// <summary>Por que ele não pode treinar (nulo = pode).</summary>
    public static string? PorQueNaoTreina(int? idade, int overall, bool temAtributosDoJogo, bool emprestado)
    {
        if (emprestado) return "emprestado: quem treina é o dono";
        if (!temAtributosDoJogo) return "sem atributos do jogo no site";
        if (idade is not int i || i > TreinoCriterios.IdadeMaxima) return $"mais de {TreinoCriterios.IdadeMaxima} anos";
        if (overall > TreinoCriterios.OverallMaximo) return $"overall acima de {TreinoCriterios.OverallMaximo}";
        return null;
    }

    /// <summary>Segunda-feira (Brasília) da semana de <paramref name="agoraBrasilia"/>: a semana vai de segunda a domingo.</summary>
    public static DateTime Semana(DateTime agoraBrasilia) =>
        agoraBrasilia.Date.AddDays(-(((int)agoraBrasilia.DayOfWeek + 6) % 7));

    /// <summary>Quando a semana fecha e o treino acontece: segunda seguinte, 0h de Brasília.</summary>
    public static DateTime FechaEm(DateTime semana) => semana.AddDays(7);

    /// <summary>
    /// Qual atributo do foco sobe (índice em <see cref="AtributosPes.Todos"/>): o que mais pesa no overall da posição
    /// dele (a fórmula do PES), entre os que ainda não estão no máximo; empate é sorteado por jogador e semana. Nulo se
    /// estão todos no máximo.
    /// </summary>
    public static int? AtributoQueSobe(FocoDoTreino foco, IReadOnlyList<int> valores, int posicaoPes, int? peUso,
        int? pePrecisao, int playerId, DateTime semana)
    {
        var nomes = TreinoCriterios.Atributos[foco];
        var candidatos = AtributosPes.Todos
            .Select((a, i) => (a.Nome, Indice: i))
            .Where(a => nomes.Contains(a.Nome) && valores[a.Indice] < AtributosPes.Maximo)
            .Select(a => a.Indice)
            .ToList();
        if (candidatos.Count == 0) return null;

        double Com(int indice)
        {
            var v = valores.ToArray();
            v[indice]++;
            return OverallPes.Calcular(v, posicaoPes, peUso, pePrecisao);
        }

        var ganhos = candidatos.Select(i => (Indice: i, Overall: Com(i))).ToList();
        var melhor = ganhos.Max(g => g.Overall);
        var empatados = ganhos.Where(g => melhor - g.Overall < 1e-9).Select(g => g.Indice).ToList();
        var sorteio = new Random(unchecked(semana.DayNumber() * 7_919 + playerId * 15_485_863));
        return empatados[sorteio.Next(empatados.Count)];
    }

    private static int DayNumber(this DateTime d) => DateOnly.FromDateTime(d).DayNumber;
}
