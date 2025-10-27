namespace Fc25Draft.Core.Entities;

public class BudgetLedger
{
    public Guid BudgetLedgerId { get; set; }

    public Guid TeamId { get; set; }

    public DateTime DataUtc { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string Origem { get; set; } = string.Empty;

    public decimal Valor { get; set; }

    public string? Descricao { get; set; }

    public Team Team { get; set; } = null!;
}
