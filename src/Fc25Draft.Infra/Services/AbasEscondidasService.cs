using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <inheritdoc />
public class AbasEscondidasService : IAbasEscondidasService
{
    private readonly DraftDbContext _db;

    public AbasEscondidasService(DraftDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<string>> ListarAsync(CancellationToken ct) =>
        await _db.AbasEscondidas.AsNoTracking().Select(a => a.Rota).ToListAsync(ct);

    public async Task SalvarAsync(IReadOnlyCollection<string> rotas, CancellationToken ct)
    {
        var novas = rotas
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var atuais = await _db.AbasEscondidas.ToListAsync(ct);
        _db.AbasEscondidas.RemoveRange(atuais.Where(a => !novas.Contains(a.Rota)));

        var jaEscondidas = atuais.Select(a => a.Rota).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var agora = DateTime.UtcNow;
        foreach (var rota in novas.Where(r => !jaEscondidas.Contains(r)))
            _db.AbasEscondidas.Add(new AbaEscondida { Rota = rota, EscondidaEm = agora });

        await _db.SaveChangesAsync(ct);
    }
}
