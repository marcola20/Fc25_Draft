namespace Fc25Draft.Core.Entities;

public class TransferHistory
{
    public Guid TransferId { get; set; }
    public Guid? CycleId { get; set; }
    public TransferType Type { get; set; }
    public int PlayerId { get; set; }
    public Guid? FromTeamId { get; set; }
    public Guid? ToTeamId { get; set; }
    public decimal? Amount { get; set; }
    public string? Notes { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime PerformedAtUtc { get; set; }
    public int? OldOverall { get; set; }
    public int? NewOverall { get; set; }

    /// <summary>
    /// Saiu do clube depois de pedir para sair: a variação do valor com que saiu, que segue no clube novo até a
    /// carência acabar (o preço não volta ao normal só por mudar de time).
    /// </summary>
    public decimal? VariacaoEmBaixa { get; set; }

    public Player Player { get; set; } = null!;
    public Team? FromTeam { get; set; }
    public Team? ToTeam { get; set; }
}
