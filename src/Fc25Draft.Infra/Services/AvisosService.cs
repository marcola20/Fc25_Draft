using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class AvisosService : IAvisosService
{
    private readonly DraftDbContext _db;

    public AvisosService(DraftDbContext db) => _db = db;

    public Task<int> ContarNaoLidosAsync(Guid teamId, CancellationToken ct) =>
        _db.AvisosTimes.AsNoTracking().CountAsync(a => a.TeamId == teamId && a.LidoEm == null, ct);

    public async Task<IReadOnlyList<AvisoDto>> ListarAsync(Guid teamId, int limite, CancellationToken ct)
    {
        var avisos = await _db.AvisosTimes.AsNoTracking()
            .Where(a => a.TeamId == teamId)
            .OrderByDescending(a => a.CriadoEm)
            .Take(limite)
            .Select(a => new { a.AvisoId, a.Tipo, a.Texto, a.Link, a.CriadoEm, Lido = a.LidoEm != null })
            .ToListAsync(ct);

        // timestamptz chega em hora local (Npgsql em modo legado): normaliza para UTC.
        return avisos
            .Select(a => new AvisoDto(a.AvisoId, a.Tipo, a.Texto, a.Link,
                a.CriadoEm.Kind == DateTimeKind.Local ? a.CriadoEm.ToUniversalTime() : a.CriadoEm, a.Lido))
            .ToList();
    }

    public async Task MarcarTodosComoLidosAsync(Guid teamId, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        await _db.AvisosTimes
            .Where(a => a.TeamId == teamId && a.LidoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LidoEm, agora), ct);
    }
}
