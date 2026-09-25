using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public class HallOfFameEntry
{
    public Guid HallOfFameId { get; set; }
    public string Descricao { get; set; } = null!;
    public TipoCompetition Tipo { get; set; } = TipoCompetition.Liga;

    /// <summary>Divisão do título de Liga (Série A ou B). Nulo em Copa, Supercopa e nas temporadas sem divisão.</summary>
    public Divisao? Divisao { get; set; }
    public string TimeCampeao { get; set; } = null!;
    public string? Tecnico { get; set; }

    /// <summary>Treinador cadastrado, quando o campeão foi dirigido por alguém da liga. Nulo para nomes soltos.</summary>
    public Guid? TreinadorId { get; set; }
    public int? Ano { get; set; }
    public string? Temporada { get; set; }
    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
