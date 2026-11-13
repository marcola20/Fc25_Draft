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

    /// <summary>Linha fina do anúncio no site: aparece no Plantão e na lista de novidades.</summary>
    public string Resumo { get; set; } = "";

    /// <summary>
    /// Versão do site, mais explicada e com exemplos, para os técnicos. Markdown simples:
    /// "## título", "- item", "1. item", "&gt; exemplo" e *negrito*.
    /// </summary>
    public string TextoSite { get; set; } = "";

    /// <summary>Quando foi publicado no site (nulo = não está no site). Vira notícia no Plantão.</summary>
    public DateTime? PublicadoEm { get; set; }

    /// <summary>Ordem dentro do grupo.</summary>
    public int Ordem { get; set; }

    /// <summary>Quando foi mandado no grupo (nulo = ainda não foi).</summary>
    public DateTime? EnviadoEm { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
