using Fc25Draft.Core.Utilities;

namespace Fc25Draft.Core.Entities;

/// <summary>Um profissional da comissão técnica contratado pelo time para a temporada (pago na hora, do caixa).</summary>
public class ComissaoContrato
{
    public Guid ContratoId { get; set; }
    public Guid TeamId { get; set; }
    public int Temporada { get; set; }
    public CargoDaComissao Cargo { get; set; }
    public decimal Valor { get; set; }
    public DateTime ContratadoEm { get; set; }

    public Team Team { get; set; } = null!;
}
