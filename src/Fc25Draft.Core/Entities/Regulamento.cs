namespace Fc25Draft.Core.Entities;

/// <summary>
/// Regulamento de uma temporada. As regras mudam de ano para ano (divisões, acesso,
/// formato da Copa), então cada temporada guarda a sua versão — normalmente criada
/// copiando a anterior e ajustando o que mudou.
/// </summary>
public class Regulamento
{
    public Guid RegulamentoId { get; set; }

    /// <summary>Ano da temporada (2009, 2010...), igual ao da Liga.</summary>
    public int Temporada { get; set; }

    public string Titulo { get; set; } = null!;

    /// <summary>Conteúdo em HTML, como aparece na página.</summary>
    public string Conteudo { get; set; } = null!;

    public DateTime CriadoEm { get; set; }
    public DateTime AtualizadoEm { get; set; }
}
