namespace Fc25Draft.Core.Entities;

/// <summary>
/// Retrato da escalação ativa de um time numa partida (titulares e banco), tirado
/// no momento em que a partida é encerrada. Os titulares são a base do contador
/// de jogos (junto com quem entrou por substituição). Competições anteriores não
/// têm esse registro.
/// </summary>
public class LigaEscalacaoPartida
{
    public Guid Id { get; set; }
    public Guid PartidaId { get; set; }
    public Guid TimeId { get; set; }
    public int JogadorId { get; set; }
    public bool Titular { get; set; }
    public int Ordem { get; set; }

    public LigaPartida Partida { get; set; } = null!;
    public Team Time { get; set; } = null!;
    public Player Jogador { get; set; } = null!;
}
