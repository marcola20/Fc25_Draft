namespace Fc25Draft.Core.Entities;

public class LigaPunicao
{
    public Guid PunicaoId { get; set; }
    public Guid LigaId { get; set; }
    public Guid TimeId { get; set; }
    public int PontosSubtraidos { get; set; }
    public string Motivo { get; set; } = null!;
    public DateTime CriadaEm { get; set; }

    public Liga Liga { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
