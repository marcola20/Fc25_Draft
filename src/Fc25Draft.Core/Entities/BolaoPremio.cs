namespace Fc25Draft.Core.Entities;

/// <summary>
/// Prêmio do bolão pago ao clube de quem ganhou: a rodada (com <see cref="RodadaId"/>) ou o geral da temporada (sem).
/// Também marca a rodada como apurada quando ninguém ganhou (sem <see cref="TreinadorId"/>) ou o vencedor estava sem
/// clube (sem <see cref="TimeId"/>, valor 0): assim ela não é apurada de novo.
/// </summary>
public class BolaoPremio
{
    public Guid PremioId { get; set; }

    /// <summary>"rodada:{rodada}:{treinador}" ou "geral:{temporada}:{treinador}" ("-" sem vencedor): paga uma vez só.</summary>
    public string Chave { get; set; } = null!;

    public int Temporada { get; set; }

    /// <summary>Rodada do bolão; nulo = prêmio do geral da temporada.</summary>
    public Guid? RodadaId { get; set; }

    public Guid? TreinadorId { get; set; }

    /// <summary>Clube que recebeu (o do vencedor na hora do pagamento); nulo = estava sem clube, não recebeu.</summary>
    public Guid? TimeId { get; set; }

    /// <summary>Colocação (1 na rodada; 1 a 3 no geral). Empate divide o prêmio das colocações que ocupam.</summary>
    public int Posicao { get; set; }

    public int Pontos { get; set; }
    public decimal Valor { get; set; }
    public string Motivo { get; set; } = null!;
    public DateTime PagoEm { get; set; }

    public Treinador? Treinador { get; set; }
    public Team? Time { get; set; }
}
