namespace Fc25Draft.Core.Entities;

/// <summary>
/// Aviso para o time (proposta recebida, aceita, recusada; lance superado; leilão vencido…). Aparece no
/// sino do topo do site para quem entrou com o token do time e na Minha Área.
/// </summary>
public class AvisoTime
{
    public Guid AvisoId { get; set; }
    public Guid TeamId { get; set; }

    /// <summary>Tipo do aviso (PROPOSTA, PROPOSTA_ACEITA, LANCE_SUPERADO…), para o ícone.</summary>
    public string Tipo { get; set; } = null!;

    public string Texto { get; set; } = null!;

    /// <summary>Página para onde o aviso leva (relativa ao site).</summary>
    public string? Link { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime? LidoEm { get; set; }

    public Team Team { get; set; } = null!;
}
