namespace Fc25Draft.Core.Entities;

/// <summary>
/// Resultado do PES enviado com a chave "segurar resultados" ligada: fica guardado sem tocar na
/// partida até o admin liberar em /admin/resultados-pendentes. Ao liberar, vira uma importação
/// normal (placar, eventos, notas, chaveamento) e a linha some.
/// </summary>
public class ResultadoPesPendente
{
    public Guid Id { get; set; }
    public string? Video { get; set; }
    public string Json { get; set; } = null!;

    // Jogo achado na validação do envio: a liberação fica nele mesmo que outro do mesmo par
    // esteja mais perto da data. Nulo quando a validação falhou (ex.: mata-mata ainda sem os times).
    public Guid? PartidaId { get; set; }
    public Guid? KnockoutJogoId { get; set; }

    // Prévia para a tela (o que a validação achou).
    public string? Competicao { get; set; }
    public string? Onde { get; set; }
    public string Casa { get; set; } = null!;
    public string Fora { get; set; } = null!;
    public int GolsCasa { get; set; }
    public int GolsFora { get; set; }
    /// <summary>Data marcada do jogo (horário de Brasília): ordem de liberação.</summary>
    public DateTime? DataHora { get; set; }
    /// <summary>Avisos e nomes não identificados da validação, uma linha cada.</summary>
    public string? Avisos { get; set; }
    /// <summary>Por que a validação (ou a última tentativa de liberar) falhou.</summary>
    public string? Erro { get; set; }

    public DateTime EnviadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}

/// <summary>Chave de /admin/resultados-pendentes (linha única).</summary>
public class ResultadoPesConfig
{
    public int Id { get; set; } // singleton, sempre 1

    /// <summary>
    /// Ligada: o resultado enviado pelo Auto_PES21 fica guardado até o admin liberar.
    /// Sem a linha, vale ligada (padrão seguro).
    /// </summary>
    public bool Segurar { get; set; } = true;

    public DateTime AtualizadoEm { get; set; }
}
