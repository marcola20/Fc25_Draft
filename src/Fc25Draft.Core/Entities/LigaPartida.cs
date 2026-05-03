using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public class LigaPartida
{
    public Guid PartidaId { get; set; }
    public Guid RodadaId { get; set; }
    public Guid TimeCasaId { get; set; }
    public Guid TimeForaId { get; set; }
    public int GolsCasa { get; set; }
    public int GolsFora { get; set; }
    public PartidaStatus Status { get; set; } = PartidaStatus.Agendada;
    public bool IsWO { get; set; }
    public bool TemPenaltis { get; set; }
    public Guid? PenaltisVencedorId { get; set; }
    public DateTime? IniciadaEm { get; set; }
    public DateTime? EncerradaEm { get; set; }

    public LigaRodada Rodada { get; set; } = null!;
    public Team TimeCasa { get; set; } = null!;
    public Team TimeFora { get; set; } = null!;
    public Team? PenaltisVencedor { get; set; }
    public ICollection<LigaEventoPartida> Eventos { get; set; } = new List<LigaEventoPartida>();
}
