namespace Fc25Draft.Core.Entities;

public class Negotiation
{
    public Guid NegotiationId { get; set; }

    public Guid OrigemTeamId { get; set; }

    public Guid DestinoTeamId { get; set; }

    public decimal? ValorOferecido { get; set; }

    public DateTime DataInicioUtc { get; set; }

    public DateTime? DataFechamentoUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Tipo { get; set; } = string.Empty;

    public string? Observacao { get; set; }

    public Team OrigemTeam { get; set; } = null!;

    public Team DestinoTeam { get; set; } = null!;

    public ICollection<NegotiationPlayer> Players { get; set; } = new List<NegotiationPlayer>();
}
