namespace Fc25Draft.Core.Entities;

public class LigaRodada
{
    public Guid RodadaId { get; set; }
    public Guid LigaId { get; set; }
    public int Numero { get; set; }

    public Liga Liga { get; set; } = null!;
    public ICollection<LigaPartida> Partidas { get; set; } = new List<LigaPartida>();
}
