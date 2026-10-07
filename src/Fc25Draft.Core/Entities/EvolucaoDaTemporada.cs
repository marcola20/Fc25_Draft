namespace Fc25Draft.Core.Entities;

/// <summary>
/// Marca que a evolução de fim de temporada (curva de idade + desempenho) já foi aplicada numa temporada. Existe
/// para ela rodar uma vez só; as mudanças vão para o save pelas <see cref="EvolucaoPes"/>.
/// </summary>
public class EvolucaoDaTemporada
{
    /// <summary>Temporada que acabou (a evolução usa os jogos e a idade dela).</summary>
    public int Temporada { get; set; }

    public DateTime AplicadaEm { get; set; }

    /// <summary>Quantos jogadores mudaram de overall.</summary>
    public int Jogadores { get; set; }
}

/// <summary>A conta da evolução de um jogador numa temporada (para a ficha explicar a variação).</summary>
public class VariacaoDaTemporada
{
    public Guid Id { get; set; }
    public int Temporada { get; set; }
    public int PlayerId { get; set; }

    /// <summary>Idade na temporada.</summary>
    public int Idade { get; set; }

    public bool SemClube { get; set; }
    public int JogosDoClube { get; set; }
    public int Titular { get; set; }
    public decimal? NotaMedia { get; set; }

    public int Curva { get; set; }
    public int Desempenho { get; set; }

    /// <summary>Pontos da evolução: ± em cada atributo afetado.</summary>
    public int Pontos { get; set; }

    /// <summary>Quanto o overall mudou pela fórmula.</summary>
    public int Variacao { get; set; }

    public int OverallAntes { get; set; }
    public int OverallDepois { get; set; }

    /// <summary>"idade 22: +1 · titular em 70%: +1".</summary>
    public string Explicacao { get; set; } = null!;

    /// <summary>A mudança de atributos que vai para o save; nula sem mudança ou sem atributos do PES.</summary>
    public int? EvolucaoPesId { get; set; }

    public Player Player { get; set; } = null!;
}
