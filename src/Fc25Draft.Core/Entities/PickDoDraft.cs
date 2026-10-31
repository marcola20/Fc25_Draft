namespace Fc25Draft.Core.Entities;

/// <summary>
/// Dono de uma pick do próximo draft que já foi negociada. Sem registro, a pick é do próprio time (o original).
/// A pick vale a posição que o time original tirar na loteria; quando o draft é gerado, quem escolhe é o dono.
/// </summary>
public class PickDoDraft
{
    public Guid PickDoDraftId { get; set; }

    /// <summary>Temporada cuja classificação define o draft (o draft do fim dessa temporada).</summary>
    public int Temporada { get; set; }

    /// <summary>1 ou 2.</summary>
    public int Rodada { get; set; }

    /// <summary>Time que tem a pick pela classificação.</summary>
    public Guid TimeOriginalId { get; set; }

    /// <summary>Quem escolhe com ela hoje.</summary>
    public Guid DonoId { get; set; }

    public DateTime NegociadaEm { get; set; }

    public Team TimeOriginal { get; set; } = null!;
    public Team Dono { get; set; } = null!;
}

/// <summary>
/// Pick do draft dentro de uma proposta. <see cref="IsTarget"/> = é do time que recebe a proposta e vai para quem
/// propôs (como nos jogadores); senão é de quem propôs e vai para o outro.
/// </summary>
public class TransferOfferPick
{
    public Guid OfferId { get; set; }
    public int Temporada { get; set; }
    public int Rodada { get; set; }
    public Guid TimeOriginalId { get; set; }
    public bool IsTarget { get; set; }

    public TransferOffer Offer { get; set; } = null!;
    public Team TimeOriginal { get; set; } = null!;
}
