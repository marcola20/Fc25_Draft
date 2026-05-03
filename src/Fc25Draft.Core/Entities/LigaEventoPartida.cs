using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public class LigaEventoPartida
{
    public Guid EventoId { get; set; }
    public Guid PartidaId { get; set; }
    public TipoEvento Tipo { get; set; }
    public Guid TimeId { get; set; }
    public int JogadorId { get; set; }
    public int? AssistenteId { get; set; }
    public int? Minuto { get; set; }
    public DateTime CriadoEm { get; set; }

    public LigaPartida Partida { get; set; } = null!;
    public Team Time { get; set; } = null!;
    public Player Jogador { get; set; } = null!;
    public Player? Assistente { get; set; }
}
