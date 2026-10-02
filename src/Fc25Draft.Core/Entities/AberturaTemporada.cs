namespace Fc25Draft.Core.Entities;

/// <summary>
/// Dia em que a temporada abre (o da Supercopa). Todo o calendário da temporada — rodadas,
/// janelas de transferência e playoff — sai desta data; cada temporada guarda a sua.
/// </summary>
public class AberturaTemporada
{
    /// <summary>Ano da temporada (2010, 2011...), igual ao da Liga.</summary>
    public int Temporada { get; set; }

    public DateTime Abertura { get; set; }

    public DateTime AtualizadoEm { get; set; }
}
