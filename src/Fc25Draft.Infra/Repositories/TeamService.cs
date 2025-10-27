using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Repositories;

public class TeamService : ITeamService
{
    private readonly DraftDbContext _db;

    public TeamService(DraftDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<Team> items, int total)> SearchAsync(string? q, int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = _db.Teams.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(t => EF.Functions.Like(t.TeamName, pattern) ||
                                     (t.OwnerName != null && EF.Functions.Like(t.OwnerName, pattern)));
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderBy(t => t.TeamName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, total);
    }

    public Task<Team?> GetAsync(Guid id)
    {
        return _db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.TeamId == id);
    }

    public async Task<Guid> CreateAsync(TeamCreateDto dto)
    {
        Validate(dto.TeamName, dto.OwnerName);

        var normalizedName = dto.TeamName.Trim();

        var exists = await _db.Teams.AnyAsync(t => t.TeamName == normalizedName);
        if (exists)
        {
            throw new InvalidOperationException($"Já existe uma equipe com o nome '{normalizedName}'.");
        }

        var entity = new Team
        {
            TeamId = Guid.NewGuid(),
            TeamName = normalizedName,
            OwnerName = NormalizeOwner(dto.OwnerName),
            TeamToken = Guid.NewGuid()
        };

        _db.Teams.Add(entity);
        await _db.SaveChangesAsync();

        return entity.TeamId;
    }

    public async Task UpdateAsync(Guid id, TeamUpdateDto dto)
    {
        Validate(dto.TeamName, dto.OwnerName);

        var entity = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == id)
                     ?? throw new KeyNotFoundException("Equipe não encontrada.");

        var normalizedName = dto.TeamName.Trim();

        var exists = await _db.Teams.AnyAsync(t => t.TeamId != id && t.TeamName == normalizedName);
        if (exists)
        {
            throw new InvalidOperationException($"Já existe uma equipe com o nome '{normalizedName}'.");
        }

        entity.TeamName = normalizedName;
        entity.OwnerName = NormalizeOwner(dto.OwnerName);

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _db.Teams.FirstOrDefaultAsync(t => t.TeamId == id)
                     ?? throw new KeyNotFoundException("Equipe não encontrada.");

        _db.Teams.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Não foi possível excluir a equipe porque ela está vinculada a outros registros.", ex);
        }
    }

    private static void Validate(string teamName, string? ownerName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
        {
            throw new ArgumentException("O nome da equipe é obrigatório.", nameof(teamName));
        }

        var trimmed = teamName.Trim();
        if (trimmed.Length is < 2 or > 80)
        {
            throw new ArgumentException("O nome da equipe deve ter entre 2 e 80 caracteres.", nameof(teamName));
        }

        if (!string.IsNullOrWhiteSpace(ownerName) && ownerName.Trim().Length > 80)
        {
            throw new ArgumentException("O nome do responsável deve ter no máximo 80 caracteres.", nameof(ownerName));
        }
    }

    private static string? NormalizeOwner(string? ownerName)
    {
        return string.IsNullOrWhiteSpace(ownerName) ? null : ownerName.Trim();
    }
}
