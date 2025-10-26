using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Repositories;

public class PlayerService : IPlayerService
{
    private readonly DraftDbContext _db;

    public PlayerService(DraftDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<Player> items, int total)> SearchAsync(string? q, short? positionId, int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var query = _db.Players.Include(p => p.Position).AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var pattern = $"%{q.Trim()}%";
            query = query.Where(p => EF.Functions.Like(p.Name, pattern));
        }

        if (positionId is { } posId)
        {
            query = query.Where(p => p.PositionId == posId);
        }

        var total = await query.CountAsync();

        var items = await query
            .OrderByDescending(p => p.Overall)
            .ThenBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return (items, total);
    }

    public Task<Player?> GetAsync(int id)
    {
        return _db.Players
            .Include(p => p.Position)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlayerId == id);
    }

    public async Task<int> CreateAsync(PlayerCreateDto dto)
    {
        Validate(dto);

        await EnsurePositionExists(dto.PositionId);

        var entity = new Player
        {
            Name = dto.Name.Trim(),
            Age = dto.Age,
            Overall = dto.Overall,
            PositionId = dto.PositionId
        };

        _db.Players.Add(entity);
        await _db.SaveChangesAsync();

        return entity.PlayerId;
    }

    public async Task UpdateAsync(int id, PlayerUpdateDto dto)
    {
        Validate(dto);

        var entity = await _db.Players.FirstOrDefaultAsync(p => p.PlayerId == id)
                     ?? throw new KeyNotFoundException("Player not found.");

        await EnsurePositionExists(dto.PositionId);

        entity.Name = dto.Name.Trim();
        entity.Age = dto.Age;
        entity.Overall = dto.Overall;
        entity.PositionId = dto.PositionId;

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.Players.FirstOrDefaultAsync(p => p.PlayerId == id)
                     ?? throw new KeyNotFoundException("Player not found.");

        _db.Players.Remove(entity);
        await _db.SaveChangesAsync();
    }

    private static void Validate(PlayerCreateDto dto)
    {
        Validate(dto.Name, dto.Age, dto.Overall, dto.PositionId);
    }

    private static void Validate(PlayerUpdateDto dto)
    {
        Validate(dto.Name, dto.Age, dto.Overall, dto.PositionId);
    }

    private static void Validate(string name, int? age, int overall, short positionId)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Player name is required.", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length is < 2 or > 80)
        {
            throw new ArgumentException("Player name must be between 2 and 80 characters.", nameof(name));
        }

        if (overall is < 0 or > 99)
        {
            throw new ArgumentException("Overall must be between 0 and 99.", nameof(overall));
        }

        if (age is < 14 or > 55)
        {
            throw new ArgumentException("Age must be between 14 and 55 when specified.", nameof(age));
        }

        if (positionId <= 0)
        {
            throw new ArgumentException("Position is required.", nameof(positionId));
        }
    }

    private async Task EnsurePositionExists(short positionId)
    {
        var exists = await _db.Positions.AnyAsync(p => p.PositionId == positionId);
        if (!exists)
        {
            throw new InvalidOperationException("Invalid position.");
        }
    }
}
