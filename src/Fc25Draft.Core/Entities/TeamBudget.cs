namespace Fc25Draft.Core.Entities;

public class TeamBudget
{
    public Guid TeamId { get; set; }

    public decimal Saldo { get; set; }

    public Team Team { get; set; } = null!;
}
