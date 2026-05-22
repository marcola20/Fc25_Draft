using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public class LigaGrupoTime
{
    public Guid Id { get; set; }
    public Guid LigaId { get; set; }
    public Guid TimeId { get; set; }
    public GrupoCopa Grupo { get; set; }

    public Liga Liga { get; set; } = null!;
    public Team Time { get; set; } = null!;
}
