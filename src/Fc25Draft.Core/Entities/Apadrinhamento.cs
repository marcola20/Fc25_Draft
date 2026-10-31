namespace Fc25Draft.Core.Entities;

/// <summary>
/// Padrinho e afilhado de um time na temporada (ver PadrinhoCriterios). Vale enquanto os dois estão no clube; se um
/// sai, o par acaba e o time pode formar outro.
/// </summary>
public class Apadrinhamento
{
    public Guid ApadrinhamentoId { get; set; }
    public Guid TeamId { get; set; }

    /// <summary>Temporada (a da Série A em andamento quando o par foi formado).</summary>
    public int Temporada { get; set; }

    public int PadrinhoId { get; set; }
    public int AfilhadoId { get; set; }
    public DateTime CriadoEm { get; set; }

    public Team Team { get; set; } = null!;
    public Player Padrinho { get; set; } = null!;
    public Player Afilhado { get; set; } = null!;
}
