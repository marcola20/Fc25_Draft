namespace Fc25Draft.Core.Entities;

/// <summary>
/// Jogador que a organização colocou no Hall da Fama. Cada temporada tem a sua turma, com até três
/// nomes em ordem (1º, 2º e 3º), como uma premiação anual. Cada jogador entra uma vez só.
/// </summary>
public class HallDaFamaJogador
{
    public Guid HallDaFamaJogadorId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>Ano da temporada da turma (ex.: 2010).</summary>
    public int Temporada { get; set; }

    /// <summary>1, 2 ou 3 dentro da turma.</summary>
    public int Colocacao { get; set; }

    /// <summary>Por que entrou, escrito pela organização (opcional).</summary>
    public string? Motivo { get; set; }

    public DateTime CriadoEm { get; set; }

    public Player Player { get; set; } = null!;
}
