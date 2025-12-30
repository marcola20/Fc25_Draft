using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Services;

public class DraftAdminService
{
    private readonly DraftDbContext _db;

    public DraftAdminService(DraftDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task UpdatePickOwnerAsync(Guid draftId, int draftPickId, Guid ownerTeamId, CancellationToken ct = default)
    {
        if (draftId == Guid.Empty) throw new ArgumentException("Draft inválido.", nameof(draftId));
        if (draftPickId <= 0) throw new ArgumentOutOfRangeException(nameof(draftPickId));
        if (ownerTeamId == Guid.Empty) throw new ArgumentException("Time inválido.", nameof(ownerTeamId));

        var pick = await _db.DraftPicks
            .FirstOrDefaultAsync(p => p.DraftId == draftId && p.OverallPick == draftPickId, ct);

        if (pick is null)
        {
            throw new KeyNotFoundException("Escolha não encontrada.");
        }

        if (pick.PlayerId.HasValue)
        {
            throw new InvalidOperationException("Não é possível alterar o responsável de uma escolha já utilizada.");
        }

        var teamExists = await _db.Teams.AnyAsync(t => t.TeamId == ownerTeamId, ct);
        if (!teamExists)
        {
            throw new KeyNotFoundException("Time não encontrado.");
        }

        pick.TeamId = ownerTeamId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SwapPickOrderAsync(Guid draftId, int draftPickIdA, int draftPickIdB, CancellationToken ct = default)
    {
        if (draftId == Guid.Empty) throw new ArgumentException("Draft inválido.", nameof(draftId));
        if (draftPickIdA <= 0 || draftPickIdB <= 0)
            throw new ArgumentOutOfRangeException(nameof(draftPickIdA), "Escolhas inválidas.");
        if (draftPickIdA == draftPickIdB)
            throw new ArgumentException("As escolhas precisam ser diferentes.");

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var picks = await _db.DraftPicks
                .AsNoTracking()
                .Where(p => p.DraftId == draftId && (p.OverallPick == draftPickIdA || p.OverallPick == draftPickIdB))
                .Select(p => new
                {
                    p.DraftId,
                    p.OverallPick,
                    p.RoundNumber,
                    p.PickInRound,
                    p.PlayerId
                })
                .ToListAsync(ct);

            if (picks.Count != 2)
            {
                throw new KeyNotFoundException("Escolha(s) não encontrada(s) para trocar.");
            }

            if (picks.Any(p => p.PlayerId.HasValue))
            {
                throw new InvalidOperationException("Não é possível trocar escolhas já utilizadas.");
            }

            var pickA = picks.First(p => p.OverallPick == draftPickIdA);
            var pickB = picks.First(p => p.OverallPick == draftPickIdB);

            var maxOverall = await _db.DraftPicks
                .AsNoTracking()
                .Where(p => p.DraftId == draftId)
                .MaxAsync(p => p.OverallPick, ct);

            var maxPickInRound = await _db.DraftPicks
                .AsNoTracking()
                .Where(p => p.DraftId == draftId && p.RoundNumber == pickA.RoundNumber)
                .MaxAsync(p => p.PickInRound, ct);

            var tempOverall = maxOverall + 1;
            var tempPickInRound = maxPickInRound + 1;

            await _db.DraftPicks
                .Where(p => p.DraftId == draftId && p.OverallPick == pickA.OverallPick)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.OverallPick, tempOverall)
                    .SetProperty(p => p.PickInRound, tempPickInRound), ct);

            await _db.DraftPicks
                .Where(p => p.DraftId == draftId && p.OverallPick == pickB.OverallPick)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.OverallPick, pickA.OverallPick)
                    .SetProperty(p => p.RoundNumber, pickA.RoundNumber)
                    .SetProperty(p => p.PickInRound, pickA.PickInRound), ct);

            await _db.DraftPicks
                .Where(p => p.DraftId == draftId && p.OverallPick == tempOverall)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.OverallPick, pickB.OverallPick)
                    .SetProperty(p => p.RoundNumber, pickB.RoundNumber)
                    .SetProperty(p => p.PickInRound, pickB.PickInRound), ct);

            await transaction.CommitAsync(ct);
        });
    }

    public async Task MovePickAsync(Guid draftId, int draftPickId, int targetOverall, CancellationToken ct = default)
    {
        if (draftId == Guid.Empty) throw new ArgumentException("Draft inválido.", nameof(draftId));
        if (draftPickId <= 0) throw new ArgumentOutOfRangeException(nameof(draftPickId));
        if (targetOverall <= 0) throw new ArgumentOutOfRangeException(nameof(targetOverall));

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var draft = await _db.Drafts
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DraftId == draftId, ct);

            if (draft is null)
            {
                throw new KeyNotFoundException("Draft não encontrado.");
            }

            var picks = await _db.DraftPicks
                .AsNoTracking()
                .Where(p => p.DraftId == draftId)
                .OrderBy(p => p.OverallPick)
                .Select(p => new { p.OverallPick, p.TeamId, p.PlayerId })
                .ToListAsync(ct);

            if (picks.Count == 0)
            {
                throw new InvalidOperationException("Draft sem escolhas para ajustar.");
            }

            if (targetOverall > picks.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(targetOverall), "O overall alvo está fora do intervalo.");
            }

            var currentIndex = picks.FindIndex(p => p.OverallPick == draftPickId);
            if (currentIndex < 0)
            {
                throw new KeyNotFoundException("Escolha não encontrada.");
            }

            var targetIndex = targetOverall - 1;
            if (currentIndex == targetIndex)
            {
                return;
            }

            var startIndex = Math.Min(currentIndex, targetIndex);
            var endIndex = Math.Max(currentIndex, targetIndex);

            if (picks.Skip(startIndex).Take(endIndex - startIndex + 1).Any(p => p.PlayerId.HasValue))
            {
                throw new InvalidOperationException("Não é possível mover escolhas já utilizadas.");
            }

            var orderedPicks = picks.Select(p => p.TeamId).ToList();
            var movingTeam = orderedPicks[currentIndex];
            orderedPicks.RemoveAt(currentIndex);
            orderedPicks.Insert(targetIndex, movingTeam);

            var rangeStartOverall = startIndex + 1;
            var rangeEndOverall = endIndex + 1;

            await _db.DraftPicks
                .Where(p => p.DraftId == draftId && p.OverallPick >= rangeStartOverall && p.OverallPick <= rangeEndOverall)
                .ExecuteDeleteAsync(ct);

            var picksPerRound = Math.Max(1, draft.TotalTeams);
            var newPicks = new List<DraftPick>(endIndex - startIndex + 1);

            for (var index = startIndex; index <= endIndex; index++)
            {
                var overall = index + 1;
                var round = (index / picksPerRound) + 1;
                var pickInRound = (index % picksPerRound) + 1;

                newPicks.Add(new DraftPick
                {
                    DraftId = draftId,
                    OverallPick = overall,
                    RoundNumber = round,
                    PickInRound = pickInRound,
                    TeamId = orderedPicks[index],
                    PlayerId = null,
                    PickedAtUtc = null
                });
            }

            _db.DraftPicks.AddRange(newPicks);
            await _db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        });
    }
}
