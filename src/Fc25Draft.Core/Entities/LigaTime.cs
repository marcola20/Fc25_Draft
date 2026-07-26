namespace Fc25Draft.Core.Entities;

/// <summary>
/// Time inscrito numa Liga (pontos corridos). Para a Copa, a inscrição é feita via <see cref="LigaGrupoTime"/>.
/// </summary>
public class LigaTime
{
    public Guid Id { get; set; }
    public Guid LigaId { get; set; }
    public Guid TimeId { get; set; }
    public Liga Liga { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
