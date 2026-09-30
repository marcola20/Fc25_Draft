namespace Fc25Draft.Core.Utilities;

/// <summary>
/// Habilidades, estilos de jogo, estilos de IA e posições do PES 2021, em português.
/// A ordem das listas é a do jogo (S1..S41, P01..P07): o índice é o bit guardado no banco.
/// </summary>
public static class HabilidadesPes
{
    public static readonly IReadOnlyList<string> Habilidades =
    [
        "Pedalada", "Toque Duplo", "Elástico", "Giro de Marselha", "Chapéu", "Giro Cruzado",
        "Corte por Trás e Giro", "Finta Escocesa", "Domínio com a Sola", "Cabeceio", "Chute de Longe",
        "Cavadinha", "Chute de Longa Distância", "Chute sem Rotação", "Folha Seca", "Chute Ascendente",
        "Finalização Acrobática", "Toque de Calcanhar", "Chute de Primeira", "Passe de Primeira",
        "Passe em Profundidade", "Passe Dosado", "Cruzamento Preciso", "Trivela", "Letra", "Passe sem Olhar",
        "Lançamento Rasteiro", "Reposição Baixa (GO)", "Reposição Alta (GO)", "Lateral Longo",
        "Reposição com a Mão (GO)", "Especialista em Pênaltis", "Pegador de Pênaltis (GO)", "Catimba",
        "Marcação Individual", "Recomposição", "Interceptação", "Corte Acrobático", "Liderança", "Talismã",
        "Espírito de Luta",
    ];

    public static readonly IReadOnlyList<string> EstilosIa =
    [
        "Driblador", "Arrancada", "Velocista", "Infiltração", "Especialista em Lançamentos",
        "Cruzamento Antecipado", "Chutador de Longe",
    ];

    // Índice = código gravado no arquivo (0 = nenhum), na ordem da tela do jogo — não a da cheat table.
    // Conferido com Cristiano Ronaldo (1 = Oportunista), Ronaldinho (9 = Camisa 10 Clássico) e com as
    // posições de quem usa cada código (1–4 atacantes, 11–14 meio-campo, 15+ defesa, 20–21 goleiros).
    public static readonly IReadOnlyList<string> EstilosDeJogo =
    [
        "-", "Oportunista", "Desmarcador", "Homem de Área", "Pivô", "Armador Criativo", "Ponta Prolífico",
        "Ala Móvel", "Especialista em Cruzamentos", "Camisa 10 Clássico", "Meia Infiltrador", "Box-to-Box",
        "Âncora", "Destruidor", "Maestro", "Lateral Ofensivo", "Lateral Defensivo", "Lateral Finalizador",
        "Atacante Extra", "Construtor", "Goleiro Ofensivo", "Goleiro Defensivo",
    ];

    /// <summary>Ordem das notas guardadas em <c>Posicoes</c> (uma letra A/B/C por posição, "-" = sem dado).</summary>
    public static IReadOnlyList<string> Posicoes => OverallPes.PosicoesPt;

    public static IEnumerable<string> Marcadas(long? mascara, IReadOnlyList<string> nomes) =>
        mascara is long m ? nomes.Where((_, i) => (m >> i & 1) == 1) : [];

    public static string EstiloDeJogo(int? codigo) =>
        codigo is int c && c >= 0 && c < EstilosDeJogo.Count ? EstilosDeJogo[c] : "-";
}
