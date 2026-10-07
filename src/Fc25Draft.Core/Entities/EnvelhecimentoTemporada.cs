namespace Fc25Draft.Core.Entities;

/// <summary>
/// Marca que os jogadores já fizeram aniversário numa temporada (todos +1 ano). Existe para o "envelhecer"
/// rodar uma vez só por temporada; o Editor PES copia a idade do site para o save.
/// </summary>
public class EnvelhecimentoTemporada
{
    /// <summary>Ano da temporada (2010...), igual ao da Liga.</summary>
    public int Temporada { get; set; }

    public DateTime AplicadoEm { get; set; }

    /// <summary>Quantos jogadores ganharam 1 ano.</summary>
    public int Jogadores { get; set; }

    /// <summary>
    /// Jogadores que não ganharam o ano por já estarem com a idade da temporada (PlayerIds separados por vírgula).
    /// O desfazer não mexe neles.
    /// </summary>
    public string? Pulados { get; set; }
}
