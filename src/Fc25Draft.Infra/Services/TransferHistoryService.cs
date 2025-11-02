using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class TransferHistoryService : ITransferHistoryService
{
    private const int MaxNotesLength = 400;
    private const int MaxPerformedByLength = 120;
    private readonly DraftDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public TransferHistoryService(
        DraftDbContext dbContext,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task RegisterTransferAsync(TransferHistory entry)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (!Enum.IsDefined(typeof(TransferType), entry.Type))
        {
            throw new ArgumentException("Tipo de transferência inválido.", nameof(entry));
        }

        var playerInfo = await _dbContext.Players
            .AsNoTracking()
            .Where(p => p.PlayerId == entry.PlayerId)
            .Select(p => new { p.PlayerId, p.Overall })
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (playerInfo is null)
        {
            throw new ArgumentException($"Jogador {entry.PlayerId} não encontrado.", nameof(entry));
        }

        if (entry.FromTeamId.HasValue)
        {
            var fromExists = await _dbContext.Teams
                .AsNoTracking()
                .AnyAsync(t => t.TeamId == entry.FromTeamId.Value)
                .ConfigureAwait(false);

            if (!fromExists)
            {
                throw new ArgumentException($"Time de origem {entry.FromTeamId} não encontrado.", nameof(entry));
            }
        }

        if (entry.ToTeamId.HasValue)
        {
            var toExists = await _dbContext.Teams
                .AsNoTracking()
                .AnyAsync(t => t.TeamId == entry.ToTeamId.Value)
                .ConfigureAwait(false);

            if (!toExists)
            {
                throw new ArgumentException($"Time de destino {entry.ToTeamId} não encontrado.", nameof(entry));
            }
        }

        if (entry.TransferId == Guid.Empty)
        {
            entry.TransferId = Guid.NewGuid();
        }

        entry.Notes = Normalize(entry.Notes, MaxNotesLength);
        entry.PerformedBy = Normalize(entry.PerformedBy, MaxPerformedByLength);
        entry.PerformedAtUtc = NormalizeToUtc(entry.PerformedAtUtc);

        await ReconcileTransferAmountAsync(entry, playerInfo.Overall).ConfigureAwait(false);

        _dbContext.TransferHistories.Add(entry);
        await _dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TransferHistory>> GetTransfersByTeamAsync(Guid teamId, int take = 50)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("teamId é obrigatório.", nameof(teamId));
        }

        var size = NormalizeTake(take);

        var teamExists = await _dbContext.Teams
            .AsNoTracking()
            .AnyAsync(t => t.TeamId == teamId)
            .ConfigureAwait(false);

        if (!teamExists)
        {
            throw new InvalidOperationException($"Time {teamId} não encontrado.");
        }

        return await _dbContext.TransferHistories
            .AsNoTracking()
            .Include(h => h.Player)
            .Include(h => h.FromTeam)
            .Include(h => h.ToTeam)
            .Where(h => h.FromTeamId == teamId || h.ToTeamId == teamId)
            .OrderByDescending(h => h.PerformedAtUtc)
            .Take(size)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TransferHistory>> GetRecentTransfersAsync(int take = 50)
    {
        var size = NormalizeTake(take);

        return await _dbContext.TransferHistories
            .AsNoTracking()
            .Include(h => h.Player)
            .Include(h => h.FromTeam)
            .Include(h => h.ToTeam)
            .OrderByDescending(h => h.PerformedAtUtc)
            .Take(size)
            .ToListAsync()
            .ConfigureAwait(false);
    }

    private static int NormalizeTake(int take)
    {
        if (take <= 0)
        {
            return 50;
        }

        return Math.Min(take, 200);
    }

    private DateTime NormalizeToUtc(DateTime value)
    {
        if (value == default)
        {
            return _timeProvider.GetUtcNow().UtcDateTime;
        }

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength];
    }

    private async Task ReconcileTransferAmountAsync(TransferHistory entry, int playerOverall)
    {
        var existingEntries = await _dbContext.TransferHistories
            .Include(h => h.Player)
            .Where(h => h.TransferId == entry.TransferId)
            .ToListAsync()
            .ConfigureAwait(false);

        if (existingEntries.Count == 0)
        {
            entry.Amount ??= 0m;
            return;
        }

        var candidates = existingEntries
            .Select(h => new TransferAmountCandidate(h, h.Player?.Overall ?? 0, false))
            .ToList();

        candidates.Add(new TransferAmountCandidate(entry, playerOverall, true));

        var amountToAssign = candidates.Max(c => c.Entry.Amount ?? 0m);
        if (entry.Amount is > 0m && entry.Amount.Value > amountToAssign)
        {
            amountToAssign = entry.Amount.Value;
        }

        if (amountToAssign <= 0m)
        {
            entry.Amount ??= 0m;
            return;
        }

        var target = candidates
            .OrderByDescending(c => c.Overall)
            .ThenByDescending(c => c.Entry.Amount ?? 0m)
            .ThenBy(c => c.IsNew ? 1 : 0)
            .First();

        foreach (var candidate in candidates)
        {
            var value = ReferenceEquals(candidate, target) ? amountToAssign : 0m;
            if (candidate.IsNew)
            {
                entry.Amount = value;
            }
            else
            {
                candidate.Entry.Amount = value;
            }
        }
    }

    private sealed record TransferAmountCandidate(TransferHistory Entry, int Overall, bool IsNew);
}
