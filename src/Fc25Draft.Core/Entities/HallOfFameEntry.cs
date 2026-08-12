using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public class HallOfFameEntry
{
    public Guid HallOfFameId { get; set; }
    public string Descricao { get; set; } = null!;
    public TipoCompetition Tipo { get; set; } = TipoCompetition.Liga;
    public string TimeCampeao { get; set; } = null!;
    public int? Ano { get; set; }
    public string? Temporada { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
