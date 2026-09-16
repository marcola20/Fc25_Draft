namespace Fc25Draft.Core.Entities;

public class LigaRodada
{
    public Guid RodadaId { get; set; }
    public Guid LigaId { get; set; }
    public int Numero { get; set; }

    /// <summary>
    /// Rodada de desempate (jogo decisivo da Copa): aparece normalmente nas rodadas,
    /// mas os resultados dela não entram na contagem de pontos da classificação.
    /// </summary>
    public bool Desempate { get; set; }

    public Liga Liga { get; set; } = null!;
    public ICollection<LigaPartida> Partidas { get; set; } = new List<LigaPartida>();
}
