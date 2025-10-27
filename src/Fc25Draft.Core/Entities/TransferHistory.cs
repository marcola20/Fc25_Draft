namespace Fc25Draft.Core.Entities;

public class TransferHistory
{
    public Guid TransferHistoryId { get; set; }

    public int PlayerId { get; set; }

    public Guid? OrigemTeamId { get; set; }

    public Guid? DestinoTeamId { get; set; }

    public decimal Valor { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public DateTime DataUtc { get; set; }

    public string? Observacao { get; set; }

    public Player Player { get; set; } = null!;

    public Team? OrigemTeam { get; set; }

    public Team? DestinoTeam { get; set; }
}
