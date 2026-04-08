using System;
using System.Collections.Generic;
using System.Linq;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Web.Services;

public class DraftService
{
    private readonly DraftDbContext _db;

    public DraftService(DraftDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<Draft> CreateDraftAsync(
        string name,
        IReadOnlyList<Guid> teamOrder,
        int totalRounds = 19,
        bool snake = false,
        IReadOnlyDictionary<int, (int? OverallMin, int? OverallMax)>? roundRules = null,
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

        if (roundRules is not null)
        {
            if (roundRules.Count != totalRounds)
            {
                var missingRounds = Enumerable.Range(1, totalRounds).Except(roundRules.Keys).ToArray();
                if (missingRounds.Length > 0)
                {
                    throw new ArgumentException($"Faltam regras de overall para as rodadas: {string.Join(", ", missingRounds)}.", nameof(roundRules));
                }
            }

            foreach (var (roundNumber, limits) in roundRules)
            {
                if (roundNumber < 1 || roundNumber > totalRounds)
                {
                    throw new ArgumentOutOfRangeException(nameof(roundRules), roundNumber, "Número de rodada inválido para o draft gerado.");
                }

                if (limits.OverallMin is int min && (min < 0 || min > 150))
                {
                    throw new ArgumentOutOfRangeException(nameof(roundRules), min, "Overall mínimo deve estar entre 0 e 150.");
                }

                if (limits.OverallMax is int max && (max < 0 || max > 150))
                {
                    throw new ArgumentOutOfRangeException(nameof(roundRules), max, "Overall máximo deve estar entre 0 e 150.");
                }

                if (limits.OverallMin is int minValue && limits.OverallMax is int maxValue && minValue > maxValue)
                {
                    throw new ArgumentException($"O overall mínimo ({minValue}) não pode ser maior que o máximo ({maxValue}) na rodada {roundNumber}.", nameof(roundRules));
                }
            }
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

            var random = Random.Shared;
            var baseOrder = teamOrder.ToArray();
            Shuffle(baseOrder, random);
            var reversedOrder = new Guid[baseOrder.Length];
            for (var i = 0; i < baseOrder.Length; i++)
            {
                reversedOrder[i] = baseOrder[baseOrder.Length - 1 - i];
            }

            for (var roundNumber = 1; roundNumber <= totalRounds; roundNumber++)
            {
                (int? OverallMin, int? OverallMax) limits = (null, null);
                if (roundRules is not null && roundRules.TryGetValue(roundNumber, out var configuredLimits))
                {
                    limits = configuredLimits;
                }

                rounds.Add(new DraftRound
                {
                    DraftId = draftId,
                    RoundNumber = roundNumber,
                    OverallMin = limits.OverallMin,
                    OverallMax = limits.OverallMax
                });

                Guid[] orderForRound;
                if (snake)
                {
                    orderForRound = roundNumber % 2 != 0 ? baseOrder : reversedOrder;
                }
                else
                {
                    orderForRound = baseOrder.ToArray();
                    Shuffle(orderForRound, random);
                }

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
        IReadOnlyDictionary<int, (int? OverallMin, int? OverallMax)>? roundRules = null,
        string? name = null,
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

        if (teamOrder.Count != 12)
        {
            throw new InvalidOperationException("O draft requer exatamente 12 equipes cadastradas.");
        }

        var existingDraft = await _db.Drafts
            .OrderByDescending(d => d.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (existingDraft is not null)
        {
            var totalExistingPicks = await _db.DraftPicks
                .Where(p => p.DraftId == existingDraft.DraftId)
                .CountAsync(ct);

            var completedExistingPicks = await _db.DraftPicks
                .Where(p => p.DraftId == existingDraft.DraftId && p.PlayerId != null)
                .CountAsync(ct);

            if (totalExistingPicks > 0 && completedExistingPicks < totalExistingPicks)
            {
                throw new InvalidOperationException("Não é possível gerar um novo draft enquanto o atual não foi concluído.");
            }
        }

        var draftName = string.IsNullOrWhiteSpace(name)
            ? $"DRAFT - {DateTime.UtcNow:yyyy-MM-dd HH:mm}"
            : name.Trim();

        return await CreateDraftAsync(draftName, teamOrder, totalRounds, snake, roundRules, ct);
    }

    public async Task<DraftRoundDetailsDto> AddRoundAsync(
        Guid draftId,
        int? overallMin,
        int? overallMax,
        CancellationToken ct = default)
    {
        if (overallMin is < 0 or > 150)
        {
            throw new ArgumentOutOfRangeException(nameof(overallMin), overallMin, "Overall mínimo deve estar entre 0 e 150.");
        }

        if (overallMax is < 0 or > 150)
        {
            throw new ArgumentOutOfRangeException(nameof(overallMax), overallMax, "Overall máximo deve estar entre 0 e 150.");
        }

        if (overallMin is int minValue && overallMax is int maxValue && minValue > maxValue)
        {
            throw new ArgumentException(
                $"O overall mínimo ({minValue}) não pode ser maior que o máximo ({maxValue}).",
                nameof(overallMin));
        }

        var strategy = _db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.DraftId == draftId, ct);
            if (draft is null)
            {
                throw new KeyNotFoundException("Draft não encontrado.");
            }

            var maxExistingRound = await _db.DraftRounds
                .Where(r => r.DraftId == draftId)
                .Select(r => (int?)r.RoundNumber)
                .MaxAsync(ct) ?? 0;

            if (maxExistingRound >= 50)
            {
                throw new InvalidOperationException("O draft não pode conter mais de 50 rodadas.");
            }

            var teamsInDraft = await _db.DraftPicks
                .Where(p => p.DraftId == draftId && p.RoundNumber == 1)
                .OrderBy(p => p.PickInRound)
                .Select(p => p.TeamId)
                .ToArrayAsync(ct);

            if (teamsInDraft.Length == 0)
            {
                throw new InvalidOperationException("Não foi possível determinar a ordem das equipes do draft.");
            }

            var nextRoundNumber = maxExistingRound + 1;

            var isSnakeDraft = false;
            if (maxExistingRound >= 2)
            {
                var secondRoundOrder = await _db.DraftPicks
                    .Where(p => p.DraftId == draftId && p.RoundNumber == 2)
                    .OrderBy(p => p.PickInRound)
                    .Select(p => p.TeamId)
                    .ToArrayAsync(ct);

                if (secondRoundOrder.Length == teamsInDraft.Length &&
                    secondRoundOrder.SequenceEqual(teamsInDraft.AsEnumerable().Reverse()))
                {
                    isSnakeDraft = true;
                }
            }

            Guid[] orderForRound;
            if (isSnakeDraft)
            {
                orderForRound = DraftService.GetRoundOrder(teamsInDraft, nextRoundNumber, true).ToArray();
            }
            else
            {
                orderForRound = teamsInDraft.ToArray();
                Shuffle(orderForRound, Random.Shared);
            }

            var teamDetails = await _db.Teams
                .Where(t => orderForRound.Contains(t.TeamId))
                .Select(t => new { t.TeamId, t.TeamName, t.OwnerName })
                .ToDictionaryAsync(t => t.TeamId, ct);

            var maxOverallPick = await _db.DraftPicks
                .Where(p => p.DraftId == draftId)
                .Select(p => (int?)p.OverallPick)
                .MaxAsync(ct) ?? 0;

            var round = new DraftRound
            {
                DraftId = draftId,
                RoundNumber = nextRoundNumber,
                OverallMin = overallMin,
                OverallMax = overallMax
            };

            var picks = new List<DraftPick>(orderForRound.Length);
            for (var index = 0; index < orderForRound.Length; index++)
            {
                var pickInRound = index + 1;
                picks.Add(new DraftPick
                {
                    DraftId = draftId,
                    RoundNumber = nextRoundNumber,
                    PickInRound = pickInRound,
                    OverallPick = maxOverallPick + index + 1,
                    TeamId = orderForRound[index],
                    PlayerId = null,
                    PickedAtUtc = null
                });
            }

            draft.TotalRounds = nextRoundNumber;

            _db.DraftRounds.Add(round);
            _db.DraftPicks.AddRange(picks);

            await _db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var pickDtos = picks
                .OrderBy(p => p.PickInRound)
                .Select(p =>
                {
                    teamDetails.TryGetValue(p.TeamId, out var team);
                    var teamName = team?.TeamName ?? string.Empty;
                    var ownerName = team?.OwnerName;

                    return new DraftRoundPickDto(
                        p.PickInRound,
                        p.OverallPick,
                        p.TeamId,
                        teamName,
                        ownerName,
                        null,
                        null,
                        null);
                })
                .ToList();

            return new DraftRoundDetailsDto(
                round.RoundNumber,
                round.OverallMin,
                round.OverallMax,
                pickDtos);
        });
    }

    public async Task RemoveRoundAsync(Guid draftId, int roundNumber, CancellationToken ct = default)
    {
        if (roundNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(roundNumber));
        }

        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);

            var draft = await _db.Drafts.FirstOrDefaultAsync(d => d.DraftId == draftId, ct);
            if (draft is null)
            {
                throw new KeyNotFoundException("Draft não encontrado.");
            }

            var maxRound = await _db.DraftRounds
                .Where(r => r.DraftId == draftId)
                .Select(r => (int?)r.RoundNumber)
                .MaxAsync(ct);

            if (maxRound is null)
            {
                throw new InvalidOperationException("O draft não possui rodadas para remover.");
            }

            if (roundNumber != maxRound.Value)
            {
                throw new InvalidOperationException("Somente a última rodada pode ser removida.");
            }

            var hasSelections = await _db.DraftPicks
                .AnyAsync(p => p.DraftId == draftId && p.RoundNumber == roundNumber && p.PlayerId != null, ct);

            if (hasSelections)
            {
                throw new InvalidOperationException("Não é possível remover uma rodada com escolhas já realizadas.");
            }

            await _db.DraftPicks
                .Where(p => p.DraftId == draftId && p.RoundNumber == roundNumber)
                .ExecuteDeleteAsync(ct);

            var affected = await _db.DraftRounds
                .Where(r => r.DraftId == draftId && r.RoundNumber == roundNumber)
                .ExecuteDeleteAsync(ct);

            if (affected == 0)
            {
                throw new KeyNotFoundException("Rodada não encontrada.");
            }

            draft.TotalRounds = maxRound.Value - 1;

            await _db.SaveChangesAsync(ct);
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

    private static void Shuffle(Guid[] items, Random random)
    {
        for (var i = items.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
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
