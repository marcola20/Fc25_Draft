using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Repositories;

public class PositionService : IPositionService
{
    private readonly DraftDbContext _db;

    public PositionService(DraftDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Position>> GetAllAsync()
    {
        return await _db.Positions
            .OrderBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync();
    }
}
