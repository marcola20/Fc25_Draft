namespace Fc25Draft.Core.Entities;

/// <summary>
/// Percentual de revenda: o clube que vendeu o jogador (<see cref="BeneficiarioTeamId"/>) recebe
/// <see cref="Percentual"/>% do valor da próxima venda feita por quem comprou (<see cref="DevedorTeamId"/>).
/// Vale uma vez: a próxima saída do jogador encerra a cláusula, pagando ou não.
/// </summary>
public class ClausulaRevenda
{
    public Guid ClausulaId { get; set; }
    public int PlayerId { get; set; }
    public Guid BeneficiarioTeamId { get; set; }
    public Guid DevedorTeamId { get; set; }
    public decimal Percentual { get; set; }

    /// <summary>Proposta em que a cláusula foi combinada.</summary>
    public Guid? OfferId { get; set; }

    public DateTime CriadaEm { get; set; }
    public DateTime? EncerradaEm { get; set; }

    /// <summary>Quanto foi pago ao encerrar (nulo ou zero quando o jogador saiu sem venda paga).</summary>
    public decimal? ValorPago { get; set; }

    public Player Player { get; set; } = null!;
    public Team Beneficiario { get; set; } = null!;
    public Team Devedor { get; set; } = null!;
}
