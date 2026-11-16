namespace Fc25Draft.Core.Entities;

/// <summary>
/// O catálogo da escalação (posições e formações) salvo em /admin/formacoes, em JSON numa linha só
/// (<see cref="IdUnico"/>). Sem linha, vale o catálogo de fábrica.
/// </summary>
public class CatalogoEscalacaoSalvo
{
    public const int IdUnico = 1;

    public int Id { get; set; } = IdUnico;

    /// <summary><see cref="Fc25Draft.Core.DTOs.CatalogoEscalacao"/> em JSON.</summary>
    public string Json { get; set; } = null!;

    public DateTime AtualizadoEm { get; set; }
}
