namespace Fc25Draft.Core.Entities;

/// <summary>
/// Nota oficial de um jogador numa partida. Partida com o registro individual do PES: a nota da liga
/// (<see cref="DaLiga"/>, calculada pelos números em <c>NotasOficiais</c>). Partida sem registro (as antigas): a nota
/// do PES tirada do JSON na importação. Titulares e quem entrou; nome do PES que não casa com o cadastro fica sem nota.
/// </summary>
public class LigaNotaJogador
{
    public Guid PartidaId { get; set; }
    public int JogadorId { get; set; }

    /// <summary>Time pelo qual ele jogou a partida (o lado do JSON).</summary>
    public Guid TimeId { get; set; }

    /// <summary>De 0 a 10 com uma casa decimal.</summary>
    public decimal Nota { get; set; }

    /// <summary>Na nota da liga, o de maior nota do jogo; na do PES, o escolhido pelo PES.</summary>
    public bool MelhorEmCampo { get; set; }

    /// <summary>true = nota da liga (pelos registros do jogo); false = nota do PES.</summary>
    public bool DaLiga { get; set; }

    public LigaPartida Partida { get; set; } = null!;
    public Player Jogador { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
