using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public class LigaKnockoutJogo
{
    public Guid KnockoutJogoId { get; set; }
    public Guid LigaId { get; set; }
    public FaseKnockout Fase { get; set; }
    public Guid? TimeCasaId { get; set; }
    public Guid? TimeForaId { get; set; }
    public Guid? VencedorId { get; set; }
    public Guid? PartidaId { get; set; }

    public Liga Liga { get; set; } = null!;
    public Team? TimeCasa { get; set; }
    public Team? TimeFora { get; set; }
    public Team? Vencedor { get; set; }
    public LigaPartida? Partida { get; set; }
}
