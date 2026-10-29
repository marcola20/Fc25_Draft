namespace Fc25Draft.Core.Entities;

/// <summary>
/// Goleiro que jogou uma partida sem escalação registrada (temporadas anteriores ao contador), apontado à mão
/// para dar o clean sheet só a ele. Fica fora da escalação para não mexer na contagem de jogos dos outros.
/// </summary>
public class LigaGoleiroPartida
{
    public Guid PartidaId { get; set; }
    public Guid TimeId { get; set; }
    public int JogadorId { get; set; }

    public LigaPartida Partida { get; set; } = null!;
    public Team Time { get; set; } = null!;
    public Player Jogador { get; set; } = null!;
}
