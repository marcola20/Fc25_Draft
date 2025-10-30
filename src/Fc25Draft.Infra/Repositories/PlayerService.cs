using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        await EnsureUniqueNameForPositionAsync(dto.Name, dto.PositionId);

        var entity = new Player
        {
            Name = dto.Name.Trim(),
            Age = dto.Age,
            Overall = dto.Overall,
            PositionId = dto.PositionId,
            PlayerGuid = Guid.NewGuid()
        };

        _db.Players.Add(entity);

        await SaveChangesEnsuringPlayerIdentityAsync();

        return entity.PlayerId;
    }

    public async Task UpdateAsync(int id, PlayerUpdateDto dto)
    {
        Validate(dto);

        var entity = await _db.Players.FirstOrDefaultAsync(p => p.PlayerId == id)
                     ?? throw new KeyNotFoundException("Jogador não encontrado.");

        await EnsurePositionExists(dto.PositionId);
        await EnsureUniqueNameForPositionAsync(dto.Name, dto.PositionId, id);

        entity.Name = dto.Name.Trim();
        entity.Age = dto.Age;
        entity.Overall = dto.Overall;
        entity.PositionId = dto.PositionId;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw TranslateDbUpdateException(ex);
        }
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.Players.FirstOrDefaultAsync(p => p.PlayerId == id)
                     ?? throw new KeyNotFoundException("Jogador não encontrado.");

        _db.Players.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task<PlayerImportResultDto> ImportCsvAsync(Stream csvStream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(csvStream);

        var errors = new List<string>();
        var playersToInsert = new List<Player>();

        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var knownPositions = await _db.Positions
            .Select(p => new { p.PositionId, p.Name })
            .ToListAsync(ct);

        var positionSet = knownPositions
            .Select(p => p.PositionId)
            .ToHashSet();

        var positionNames = knownPositions.ToDictionary(p => p.PositionId, p => p.Name);

        var existingPlayerKeys = await _db.Players
            .Select(p => new { p.Name, p.PositionId })
            .ToListAsync(ct);

        var uniquePlayerKeys = new HashSet<(string Name, short PositionId)>(
            existingPlayerKeys.Select(p => (p.Name, p.PositionId)));

        var lineNumber = 0;

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync();
            if (line is null)
            {
                continue;
            }

            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(';');

            if (parts.Length < 4)
            {
                errors.Add($"Linha {lineNumber}: formato inválido. Utilize \"Nome;Idade;Overall;PositionId\".");
                continue;
            }

            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
            }

            if (lineNumber == 1 && parts[0].Equals("name", StringComparison.OrdinalIgnoreCase))
            {
                // Linha de cabeçalho
                continue;
            }

            var name = parts[0];
            var ageText = parts[1];
            var overallText = parts[2];
            var positionText = parts[3];

            int? age = null;
            if (!string.IsNullOrEmpty(ageText))
            {
                if (int.TryParse(ageText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedAge))
                {
                    age = parsedAge;
                }
                else
                {
                    errors.Add($"Linha {lineNumber}: idade inválida.");
                    continue;
                }
            }

            if (!int.TryParse(overallText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var overall))
            {
                errors.Add($"Linha {lineNumber}: overall inválido.");
                continue;
            }

            if (!short.TryParse(positionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var positionId))
            {
                errors.Add($"Linha {lineNumber}: posição inválida.");
                continue;
            }

            try
            {
                Validate(name, age, overall, positionId);
            }
            catch (ArgumentException ex)
            {
                errors.Add($"Linha {lineNumber}: {ex.Message}");
                continue;
            }

            if (!positionSet.Contains(positionId))
            {
                errors.Add($"Linha {lineNumber}: posição {positionId} não encontrada.");
                continue;
            }

            var normalizedName = name.Trim();

            if (!uniquePlayerKeys.Add((normalizedName, positionId)))
            {
                var positionLabel = positionNames.TryGetValue(positionId, out var posName)
                    ? posName
                    : positionId.ToString(CultureInfo.InvariantCulture);

                errors.Add($"Linha {lineNumber}: já existe um jogador com o nome \"{normalizedName}\" na posição {positionLabel}.");
                continue;
            }

            playersToInsert.Add(new Player
            {
                Name = normalizedName,
                Age = age,
                Overall = overall,
                PositionId = positionId,
                PlayerGuid = Guid.NewGuid()
            });
        }

        if (playersToInsert.Count == 0)
        {
            return new PlayerImportResultDto(0, errors);
        }

        await _db.Players.AddRangeAsync(playersToInsert, ct);
        await SaveChangesEnsuringPlayerIdentityAsync(ct);

        return new PlayerImportResultDto(playersToInsert.Count, errors);
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
            throw new ArgumentException("O nome do jogador é obrigatório.", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length is < 2 or > 80)
        {
            throw new ArgumentException("O nome do jogador deve ter entre 2 e 80 caracteres.", nameof(name));
        }

        if (overall is < 0 or > 99)
        {
            throw new ArgumentException("O overall deve estar entre 0 e 99.", nameof(overall));
        }

        if (age is < 14 or > 55)
        {
            throw new ArgumentException("A idade deve estar entre 14 e 55 anos quando informada.", nameof(age));
        }

        if (positionId <= 0)
        {
            throw new ArgumentException("A posição é obrigatória.", nameof(positionId));
        }
    }

    private async Task EnsurePositionExists(short positionId)
    {
        var exists = await _db.Positions.AnyAsync(p => p.PositionId == positionId);
        if (!exists)
        {
            throw new InvalidOperationException("Posição inválida.");
        }
    }

    private async Task EnsureUniqueNameForPositionAsync(string name, short positionId, int? ignorePlayerId = null)
    {
        var normalizedName = name.Trim();

        var query = _db.Players.AsNoTracking()
            .Where(p => p.PositionId == positionId && p.Name == normalizedName);

        if (ignorePlayerId is int id)
        {
            query = query.Where(p => p.PlayerId != id);
        }

        var exists = await query.AnyAsync();
        if (exists)
        {
            throw new InvalidOperationException("Já existe um jogador com este nome nesta posição.");
        }
    }

    private async Task SaveChangesEnsuringPlayerIdentityAsync(CancellationToken ct = default)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsPlayerPrimaryKeyViolation(ex))
        {
            await ResetPlayerIdentitySequenceAsync(ct);

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException retryEx)
            {
                throw TranslateDbUpdateException(retryEx);
            }
        }
        catch (DbUpdateException ex)
        {
            throw TranslateDbUpdateException(ex);
        }
    }

    private static bool IsPlayerPrimaryKeyViolation(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgres)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(postgres.ConstraintName))
        {
            return string.Equals(postgres.ConstraintName, "PK_Players", StringComparison.OrdinalIgnoreCase);
        }

        if (!string.Equals(postgres.SqlState, PostgresErrorCodes.UniqueViolation, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.Equals(postgres.TableName, "Players", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(postgres.ColumnName) ||
               string.Equals(postgres.ColumnName, "PlayerId", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ResetPlayerIdentitySequenceAsync(CancellationToken ct)
    {
        var maxId = await _db.Players
            .AsNoTracking()
            .MaxAsync(p => (int?)p.PlayerId, ct) ?? 0;

        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT setval(pg_get_serial_sequence('"Players"', 'PlayerId'), {maxId})",
            ct);
    }

    private static InvalidOperationException TranslateDbUpdateException(DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException postgres)
        {
            var constraintName = postgres.ConstraintName;

            if (!string.IsNullOrWhiteSpace(constraintName))
            {
                switch (constraintName.ToUpperInvariant())
                {
                    case "IX_PLAYERS_NAME_POSITIONID":
                        return new InvalidOperationException("Já existe um jogador com este nome nesta posição.");
                    case "IX_PLAYERS_PLAYERGUID":
                        return new InvalidOperationException("Já existe um jogador com este identificador.");
                    case "FK_PLAYERS_POSITIONS_POSITIONID":
                        return new InvalidOperationException("Posição informada não existe.");
                    case "FK_PLAYERS_TEAMS_CURRENTTEAMID":
                        return new InvalidOperationException("Time atual informado é inválido.");
                }
            }

            if (postgres.SqlState == PostgresErrorCodes.UniqueViolation)
            {
                return new InvalidOperationException(FormatUniqueViolationMessage(postgres));
            }

            if (postgres.SqlState == PostgresErrorCodes.ForeignKeyViolation)
            {
                return new InvalidOperationException("Os dados informados violam uma restrição de relacionamento.");
            }

            if (postgres.SqlState == PostgresErrorCodes.CheckViolation)
            {
                return new InvalidOperationException("Os dados informados violam uma regra de validação.");
            }

            return new InvalidOperationException($"Erro do banco de dados ({postgres.SqlState}): {postgres.MessageText}");
        }

        return new InvalidOperationException("Não foi possível salvar o jogador. Detalhes: " + exception.GetBaseException().Message);
    }

    private static string FormatUniqueViolationMessage(PostgresException postgres)
    {
        if (!string.IsNullOrWhiteSpace(postgres.ConstraintName))
        {
            return $"Os dados informados violam a restrição única '{postgres.ConstraintName}'.";
        }

        if (!string.IsNullOrWhiteSpace(postgres.Detail))
        {
            var detail = postgres.Detail.Trim();

            const string prefix = "Key (";
            const string separator = ")=(";
            const string suffix = ") already exists.";

            if (detail.StartsWith(prefix, StringComparison.Ordinal) && detail.Contains(separator, StringComparison.Ordinal))
            {
                var keyStart = prefix.Length;
                var separatorIndex = detail.IndexOf(separator, StringComparison.Ordinal);
                var key = detail.Substring(keyStart, separatorIndex - keyStart);

                var valueStart = separatorIndex + separator.Length;
                var valueEnd = detail.IndexOf(suffix, valueStart, StringComparison.OrdinalIgnoreCase);
                if (valueEnd < 0)
                {
                    valueEnd = detail.Length;
                }

                var value = detail.Substring(valueStart, valueEnd - valueStart).TrimEnd(')');
                return $"Os dados informados violam uma restrição de unicidade. Chave duplicada: ({key}) = ({value}).";
            }

            return $"Os dados informados violam uma restrição de unicidade. Detalhes: {detail}";
        }

        return "Os dados informados violam uma restrição de unicidade.";
    }
}
