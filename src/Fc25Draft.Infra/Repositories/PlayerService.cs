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
            PositionId = dto.PositionId,
            PlayerGuid = Guid.NewGuid(),
        };

        _db.Players.Add(entity);
        await _db.SaveChangesAsync();

        return entity.PlayerId;
    }

    public async Task UpdateAsync(int id, PlayerUpdateDto dto)
    {
        Validate(dto);

        var entity = await _db.Players.FirstOrDefaultAsync(p => p.PlayerId == id)
                     ?? throw new KeyNotFoundException("Jogador não encontrado.");

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
                     ?? throw new KeyNotFoundException("Jogador não encontrado.");

        var blockers = await GetDeleteBlockersAsync(id);
        if (blockers.Count > 0)
        {
            throw new InvalidOperationException(
                $"Não é possível excluir \"{entity.Name}\" porque ele está vinculado a: {string.Join(", ", blockers)}. " +
                "Remova esses vínculos antes de excluir o jogador.");
        }

        _db.Players.Remove(entity);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException(
                $"Não foi possível excluir \"{entity.Name}\" porque ele está vinculado a outros registros.", ex);
        }
    }

    private async Task<List<string>> GetDeleteBlockersAsync(int id)
    {
        var blockers = new List<string>();

        async Task CheckAsync<T>(IQueryable<T> query, string singular, string plural)
        {
            var count = await query.CountAsync();
            if (count > 0)
            {
                blockers.Add(count == 1 ? $"1 {singular}" : $"{count} {plural}");
            }
        }

        await CheckAsync(_db.TeamRosters.Where(r => r.PlayerId == id),
            "elenco de time", "elencos de times");

        await CheckAsync(_db.DraftPicks.Where(d => d.PlayerId == id),
            "escolha de draft", "escolhas de draft");

        await CheckAsync(_db.TeamLineupSlots.Where(s => s.PlayerId == id),
            "escalação", "escalações");

        await CheckAsync(_db.TeamLineups.Where(l =>
                l.CaptainPlayerId == id ||
                l.ShortFreeKick1PlayerId == id ||
                l.ShortFreeKick2PlayerId == id ||
                l.LongFreeKickPlayerId == id ||
                l.PenaltiesPlayerId == id ||
                l.CornerLeftPlayerId == id ||
                l.CornerRightPlayerId == id ||
                l.AttackingPlayer1Id == id ||
                l.AttackingPlayer2Id == id ||
                l.AttackingPlayer3Id == id)
            .Select(l => l.LineupId),
            "função de escalação (capitão/cobrador)", "funções de escalação (capitão/cobrador)");

        await CheckAsync(_db.MarketItems.Where(m => m.PlayerId == id),
            "item de mercado", "itens de mercado");

        await CheckAsync(_db.MarketTransactions.Where(t => t.PlayerId == id),
            "transação de mercado", "transações de mercado");

        await CheckAsync(_db.TransferHistories.Where(h => h.PlayerId == id),
            "registro no histórico de transferências", "registros no histórico de transferências");

        await CheckAsync(_db.TransferOfferPlayers.Where(o => o.PlayerId == id),
            "proposta de troca", "propostas de troca");

        return blockers;
    }

    public async Task<PlayerImportResultDto> ImportCsvAsync(Stream csvStream, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(csvStream);

        var errors = new List<string>();
        var playersToInsert = new List<Player>();

        using var reader = new StreamReader(csvStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        var knownPositions = await _db.Positions
            .Select(p => p.PositionId)
            .ToListAsync(ct);

        var positionSet = knownPositions.ToHashSet();

        var lineNumber = 0;
        char? detectedSeparator = null;

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

            // Detecta o separador automaticamente na primeira linha não-vazia (cabeçalho ou dados)
            if (detectedSeparator is null)
            {
                var semicolons = line.Count(c => c == ';');
                var commas = line.Count(c => c == ',');
                detectedSeparator = commas > semicolons ? ',' : ';';
            }

            var parts = line.Split(detectedSeparator.Value);

            if (parts.Length < 4)
            {
                errors.Add($"Linha {lineNumber}: formato inválido. Utilize \"Nome{detectedSeparator}Idade{detectedSeparator}Overall{detectedSeparator}PositionId\".");
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

            playersToInsert.Add(new Player
            {
                Name = name.Trim(),
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
        await _db.SaveChangesAsync(ct);

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
}
