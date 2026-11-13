namespace Fc25Draft.Core.Entities;

/// <summary>
/// Texto de anúncio de uma novidade, pronto para colar no grupo do WhatsApp, junto com a
/// explicação completa da regra (o que o anúncio não diz) para o admin responder as dúvidas.
/// </summary>
public class Anuncio
{
    public Guid AnuncioId { get; set; }

    /// <summary>Leva do anúncio ("Para agora", "Virada 2011"...), usada para agrupar na tela.</summary>
    public string Grupo { get; set; } = null!;

    public string Titulo { get; set; } = null!;

    /// <summary>Texto no formato do WhatsApp (*negrito*, _itálico_), como vai para o grupo.</summary>
    public string Texto { get; set; } = "";

    /// <summary>Explicação para o admin: regra completa, casos de borda e onde fica no site.</summary>
    public string Explicacao { get; set; } = "";

    /// <summary>Ordem dentro do grupo.</summary>
    public int Ordem { get; set; }

    /// <summary>Quando foi mandado no grupo (nulo = ainda não foi).</summary>
    public DateTime? EnviadoEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
