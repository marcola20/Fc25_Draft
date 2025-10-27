namespace Fc25Draft.Core.Entities;

public class NegotiationPlayer
{
    public Guid NegotiationPlayerId { get; set; }

    public Guid NegotiationId { get; set; }

    public int PlayerId { get; set; }

    public Guid TeamId { get; set; }

    public string Papel { get; set; } = string.Empty;

    public Negotiation Negotiation { get; set; } = null!;

    public Player Player { get; set; } = null!;

    public Team Team { get; set; } = null!;
}
