using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace Fc25Draft.Web.Services;

public class DraftService
{
    private readonly DraftDbContext _db;

    public DraftService(DraftDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task ResetDraftAsync(CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            await _db.TeamRosters.ExecuteDeleteAsync(ct);
            await _db.DraftPicks.ExecuteDeleteAsync(ct);
            await _db.DraftRounds.ExecuteDeleteAsync(ct);
            await _db.Drafts.ExecuteDeleteAsync(ct);

            await _db.Players.ExecuteDeleteAsync(ct);
            await _db.Teams.ExecuteDeleteAsync(ct);

            await _db.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Players', RESEED, 0);", cancellationToken: ct);

            await SeedData.SeedAsync(_db, ct);

            await transaction.CommitAsync(ct);
        });
    }

    public async Task<Draft> CreateDraftAsync(
        string name,
        IReadOnlyList<Guid> teamOrder,
        int totalRounds = 19,
        bool snake = false,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(teamOrder);

        if (teamOrder.Count != 14)
        {
            throw new ArgumentException("Drafts must contain exactly 14 teams.", nameof(teamOrder));
        }

        if (totalRounds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRounds), totalRounds, "Total rounds must be greater than zero.");
        }

        var distinctTeamIds = teamOrder.Distinct().ToArray();
        var existingTeamIds = await _db.Teams
            .Where(team => distinctTeamIds.Contains(team.TeamId))
            .Select(team => team.TeamId)
            .ToListAsync(ct);

        if (existingTeamIds.Count != distinctTeamIds.Length)
        {
            var missingIds = distinctTeamIds.Except(existingTeamIds).ToArray();
            throw new ArgumentException($"The following teams do not exist: {string.Join(", ", missingIds)}", nameof(teamOrder));
        }

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var draftId = Guid.NewGuid();
            var utcNow = DateTime.UtcNow;

            var draft = new Draft
            {
                DraftId = draftId,
                Name = name,
                TotalTeams = teamOrder.Count,
                TotalRounds = totalRounds,
                CreatedAtUtc = utcNow
            };

            var rounds = new List<DraftRound>(totalRounds);
            var picks = new List<DraftPick>(totalRounds * teamOrder.Count);

            var baseOrder = teamOrder.ToArray();
            var reversedOrder = Array.Empty<Guid>();

            if (snake)
            {
                reversedOrder = new Guid[baseOrder.Length];
                for (var i = 0; i < baseOrder.Length; i++)
                {
                    reversedOrder[i] = baseOrder[baseOrder.Length - 1 - i];
                }
            }

            for (var roundNumber = 1; roundNumber <= totalRounds; roundNumber++)
            {
                rounds.Add(new DraftRound
                {
                    DraftId = draftId,
                    RoundNumber = roundNumber
                });

                var orderForRound = (!snake || roundNumber % 2 != 0) ? baseOrder : reversedOrder;

                for (var pickIndex = 0; pickIndex < baseOrder.Length; pickIndex++)
                {
                    var pickInRound = pickIndex + 1;

                    picks.Add(new DraftPick
                    {
                        DraftId = draftId,
                        RoundNumber = roundNumber,
                        PickInRound = pickInRound,
                        OverallPick = ((roundNumber - 1) * baseOrder.Length) + pickInRound,
                        TeamId = orderForRound[pickIndex],
                        PlayerId = null,
                        PickedAtUtc = null
                    });
                }
            }

            draft.Rounds = rounds;
            draft.Picks = picks;

            _db.Drafts.Add(draft);
            _db.DraftRounds.AddRange(rounds);
            _db.DraftPicks.AddRange(picks);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return draft;
        });
    }

    public async Task<Draft> GenerateDraftAsync(
        int totalRounds,
        bool snake = false,
        CancellationToken ct = default)
    {
        if (totalRounds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalRounds), totalRounds, "Total rounds must be greater than zero.");
        }

        var teamOrder = await _db.Teams
            .OrderBy(t => t.TeamName)
            .Select(t => t.TeamId)
            .ToListAsync(ct);

        if (teamOrder.Count == 0)
        {
            throw new InvalidOperationException("Nenhuma equipe cadastrada para gerar o draft.");
        }

        if (teamOrder.Count != 14)
        {
            throw new InvalidOperationException("O draft requer exatamente 14 equipes cadastradas.");
        }

        await ClearDraftDataAsync(ct);

        var name = $"FC25 Draft - {DateTime.UtcNow:yyyy-MM-dd HH:mm}";
        return await CreateDraftAsync(name, teamOrder, totalRounds, snake, ct);
    }

    private async Task ClearDraftDataAsync(CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            await _db.TeamRosters.ExecuteDeleteAsync(ct);
            await _db.DraftPicks.ExecuteDeleteAsync(ct);
            await _db.DraftRounds.ExecuteDeleteAsync(ct);
            await _db.Drafts.ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);
        });
    }

    public static IReadOnlyList<Guid> GetRoundOrder(IReadOnlyList<Guid> baseOrder, int roundNumber, bool snake)
    {
        ArgumentNullException.ThrowIfNull(baseOrder);

        if (!snake || roundNumber % 2 != 0)
        {
            return baseOrder;
        }

        var reversed = new Guid[baseOrder.Count];
        for (var i = 0; i < baseOrder.Count; i++)
        {
            reversed[i] = baseOrder[baseOrder.Count - 1 - i];
        }

        return reversed;
    }
}

/*
Example usage (e.g., from a component or a seed class):

var teamOrder = await dbContext.Teams
    .OrderBy(t => t.TeamName)
    .Select(t => t.TeamId)
    .ToListAsync(ct);

var draft = await draftService.CreateDraftAsync(
    "FC25 Draft - Temporada 2025",
    teamOrder,
    totalRounds: 19,
    snake: true,
    ct);
*/
