namespace Fc25Draft.Core.Entities;

/// <summary>
/// Nota de um jogador numa partida, tirada do JSON do PES na importação (ou do JSON já guardado).
/// Titulares e quem entrou; nome do PES que não casa com o cadastro fica sem nota.
/// </summary>
public class LigaNotaJogador
{
    public Guid PartidaId { get; set; }
    public int JogadorId { get; set; }

    /// <summary>Time pelo qual ele jogou a partida (o lado do JSON).</summary>
    public Guid TimeId { get; set; }

    /// <summary>Nota do PES, de 0 a 10 com uma casa decimal.</summary>
    public decimal Nota { get; set; }

    public bool MelhorEmCampo { get; set; }

    public LigaPartida Partida { get; set; } = null!;
    public Player Jogador { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
