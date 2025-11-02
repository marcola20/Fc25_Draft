using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class MarketItemGenerationService : IMarketItemGenerationService
{
    private readonly DraftDbContext _dbContext;
    private readonly IPricingService _pricingService;
    private readonly TimeProvider _timeProvider;

    public MarketItemGenerationService(
        DraftDbContext dbContext,
        IPricingService pricingService,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MarketItemGenerationPreview> PreviewAsync(Guid cycleId, MarketItemGenerationOptions options, CancellationToken ct)
    {
        var context = await PrepareAsync(cycleId, options, ct).ConfigureAwait(false);
        var items = await BuildItemsAsync(context, ct).ConfigureAwait(false);

        return new MarketItemGenerationPreview(
            context.CycleId,
            options.DesiredCount,
            context.EligibleCount,
            context.Seed,
            items,
            Array.Empty<MarketItemGenerationSkip>(),
            context.FirstExpirationUtc,
            context.LastExpirationUtc);
    }

    public async Task<MarketItemGenerationResult> GenerateAsync(Guid cycleId, MarketItemGenerationOptions options, CancellationToken ct)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            try
            {
                var context = await PrepareAsync(cycleId, options, ct).ConfigureAwait(false);

                var existingPlayerIds = await _dbContext.MarketItems
                    .AsNoTracking()
                    .Where(i => i.CycleId == cycleId)
                    .Select(i => i.PlayerId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var existingSet = new HashSet<int>(existingPlayerIds);
                var createdItems = new List<MarketItemGenerationItem>(context.SelectedPlayers.Count);
                var skipped = new List<MarketItemGenerationSkip>();
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                foreach (var selected in context.SelectedPlayers)
                {
                    var playerId = selected.Candidate.PlayerId;
                    if (existingSet.Contains(playerId))
                    {
                        skipped.Add(new MarketItemGenerationSkip(playerId, selected.Candidate.PlayerName, "Jogador já listado no ciclo."));
                        continue;
                    }

                    var (pricing, age) = await CalculatePricingAsync(selected, ct).ConfigureAwait(false);

                    var entity = new MarketItem
                    {
                        ItemId = Guid.NewGuid(),
                        CycleId = cycleId,
                        PlayerId = playerId,
                        BasePrice = pricing.BasePrice,
                        BuyNowPrice = pricing.BuyNowPrice,
                        MinIncrement = pricing.MinIncrement,
                        ExpiresAtUtc = selected.ExpiresAtUtc,
                        Status = MarketItemStatus.Draft,
                        CreatedAtUtc = now,
                        LastUpdateUtc = now
                    };

                    await _dbContext.MarketItems.AddAsync(entity, ct).ConfigureAwait(false);
                    existingSet.Add(playerId);

                    createdItems.Add(new MarketItemGenerationItem(
                        playerId,
                        selected.Candidate.PlayerName,
                        selected.Candidate.PositionId,
                        selected.Candidate.PositionName,
                        selected.Candidate.Overall,
                        selected.Candidate.Age ?? age,
                        selected.Candidate.TeamId,
                        selected.Candidate.TeamName,
                        pricing.BasePrice,
                        pricing.BuyNowPrice,
                        pricing.MinIncrement,
                        selected.ExpiresAtUtc));
                }

                if (createdItems.Count > 0)
                {
                    try
                    {
                        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                    catch (DbUpdateException)
                    {
                        var pendingIds = createdItems.Select(i => i.PlayerId).ToArray();
                        var duplicatedPlayers = await _dbContext.MarketItems
                            .AsNoTracking()
                            .Where(i => i.CycleId == cycleId && pendingIds.Contains(i.PlayerId))
                            .Select(i => i.PlayerId)
                            .ToListAsync(ct)
                            .ConfigureAwait(false);

                        if (duplicatedPlayers.Count > 0)
                        {
                            foreach (var entry in _dbContext.ChangeTracker.Entries<MarketItem>()
                                         .Where(e => e.State == EntityState.Added && duplicatedPlayers.Contains(e.Entity.PlayerId))
                                         .ToList())
                            {
                                entry.State = EntityState.Detached;
                            }

                            foreach (var duplicate in createdItems.Where(i => duplicatedPlayers.Contains(i.PlayerId)).ToList())
                            {
                                skipped.Add(new MarketItemGenerationSkip(duplicate.PlayerId, duplicate.PlayerName, "Jogador já listado no ciclo."));
                                createdItems.Remove(duplicate);
                            }
                        }

                        if (createdItems.Count > 0)
                        {
                            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                await tx.CommitAsync(ct).ConfigureAwait(false);

                return new MarketItemGenerationResult(
                    context.CycleId,
                    options.DesiredCount,
                    context.EligibleCount,
                    context.Seed,
                    createdItems.Count,
                    createdItems,
                    skipped,
                    createdItems.Count == 0 ? null : createdItems.Min(i => i.ExpiresAtUtc),
                    createdItems.Count == 0 ? null : createdItems.Max(i => i.ExpiresAtUtc));
            }
            catch
            {
                await tx.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
        }).ConfigureAwait(false);
    }

    public async Task<int> DeleteDraftsAsync(Guid cycleId, CancellationToken ct)
    {
        var cycle = await _dbContext.MarketCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CycleId == cycleId, ct)
            .ConfigureAwait(false);

        if (cycle is null)
        {
            throw new MarketNotFoundException("Ciclo não encontrado.");
        }

        if (cycle.Status != MarketCycleStatus.Draft)
        {
            throw new MarketValidationException("Apenas ciclos em rascunho podem ter itens removidos.");
        }

        var items = await _dbContext.MarketItems
            .Where(i => i.CycleId == cycleId && i.Status == MarketItemStatus.Draft)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (items.Count == 0)
        {
            return 0;
        }

        _dbContext.MarketItems.RemoveRange(items);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        return items.Count;
    }

    private async Task<GenerationContext> PrepareAsync(Guid cycleId, MarketItemGenerationOptions options, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DesiredCount <= 0)
        {
            throw new MarketValidationException("A quantidade desejada deve ser maior que zero.");
        }

        if (options.MinOverall.HasValue && options.MaxOverall.HasValue && options.MinOverall.Value > options.MaxOverall.Value)
        {
            throw new MarketValidationException("O overall mínimo deve ser menor ou igual ao máximo.");
        }

        if (options.MaxPerTeam.HasValue && options.MaxPerTeam.Value < 0)
        {
            throw new MarketValidationException("O limite por time deve ser maior ou igual a zero.");
        }

        if (options.MaxPerPosition.HasValue && options.MaxPerPosition.Value < 0)
        {
            throw new MarketValidationException("O limite por posição deve ser maior ou igual a zero.");
        }

        var cycle = await _dbContext.MarketCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CycleId == cycleId, ct)
            .ConfigureAwait(false)
            ?? throw new MarketNotFoundException("Ciclo não encontrado.");

        if (cycle.Status != MarketCycleStatus.Draft)
        {
            throw new MarketValidationException("Itens só podem ser gerados para ciclos em rascunho.");
        }

        var seed = options.Seed ?? Random.Shared.Next();
        var excluded = await LoadExcludedPlayersAsync(cycleId, options, ct).ConfigureAwait(false);
        var eligiblePlayers = await QueryEligiblePlayersAsync(options, excluded, ct).ConfigureAwait(false);

        if (eligiblePlayers.Count == 0)
        {
            throw new MarketValidationException("Nenhum jogador elegível para os filtros informados.");
        }

        var maxSelectable = CalculateMaxSelectable(eligiblePlayers, options.MaxPerTeam, options.MaxPerPosition);
        if (maxSelectable == 0)
        {
            throw new MarketValidationException("Nenhum jogador atende às regras de geração.");
        }

        if (options.DesiredCount > maxSelectable)
        {
            throw new MarketValidationException($"A quantidade desejada ({options.DesiredCount}) é maior do que o total elegível ({maxSelectable}).");
        }

        var selectedCandidates = SelectCandidates(eligiblePlayers, options, seed);
        if (selectedCandidates.Count < options.DesiredCount)
        {
            throw new MarketValidationException("Não foi possível selecionar jogadores suficientes respeitando os limites configurados.");
        }

        var expirations = BuildExpirationSchedule(selectedCandidates.Count, cycle, options, seed);
        var multipliers = CalculateScarcityMultipliers(eligiblePlayers, selectedCandidates);
        var selected = selectedCandidates
            .Select((candidate, index) => new SelectedCandidate(
                candidate,
                expirations[index],
                multipliers.TryGetValue(candidate.PlayerId, out var multiplier) ? multiplier : 1m))
            .ToList();

        return new GenerationContext(
            cycle.CycleId,
            seed,
            eligiblePlayers.Count,
            selected,
            selected.Count == 0 ? null : selected.Min(s => s.ExpiresAtUtc),
            selected.Count == 0 ? null : selected.Max(s => s.ExpiresAtUtc));
    }

    private async Task<HashSet<int>> LoadExcludedPlayersAsync(Guid cycleId, MarketItemGenerationOptions options, CancellationToken ct)
    {
        var excluded = new HashSet<int>();

        if (options.EnsureUniquePlayerPerCycle)
        {
            var playersInCycle = await _dbContext.MarketItems
                .AsNoTracking()
                .Where(i => i.CycleId == cycleId)
                .Select(i => i.PlayerId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var playerId in playersInCycle)
            {
                excluded.Add(playerId);
            }
        }

        if (options.ExcludeAlreadyListedInOpenCycles)
        {
            var openCyclePlayers = await _dbContext.MarketItems
                .AsNoTracking()
                .Where(i => i.Cycle.Status == MarketCycleStatus.Active && i.Status == MarketItemStatus.Active)
                .Select(i => i.PlayerId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var playerId in openCyclePlayers)
            {
                excluded.Add(playerId);
            }
        }

        return excluded;
    }

    private async Task<List<MarketItemGenerationCandidate>> QueryEligiblePlayersAsync(
        MarketItemGenerationOptions options,
        HashSet<int> excluded,
        CancellationToken ct)
    {
        var query = _dbContext.Players
            .AsNoTracking()
            .Where(player => !player.TeamRosters.Any())
            .Select(player => new
            {
                Player = player,
                Team = player.TeamRosters
                    .Select(r => new { r.TeamId, r.Team.TeamName })
                    .FirstOrDefault()
            });

        if (excluded.Count > 0)
        {
            query = query.Where(p => !excluded.Contains(p.Player.PlayerId));
        }

        if (options.PositionIds is { Count: > 0 })
        {
            var positions = options.PositionIds.Distinct().ToArray();
            query = query.Where(p => positions.Contains(p.Player.PositionId));
        }

        if (options.MinOverall.HasValue)
        {
            query = query.Where(p => p.Player.Overall >= options.MinOverall.Value);
        }

        if (options.MaxOverall.HasValue)
        {
            query = query.Where(p => p.Player.Overall <= options.MaxOverall.Value);
        }

        if (options.MinAge.HasValue)
        {
            query = query.Where(p => p.Player.Age.HasValue && p.Player.Age.Value >= options.MinAge.Value);
        }

        if (options.MaxAge.HasValue)
        {
            query = query.Where(p => p.Player.Age.HasValue && p.Player.Age.Value <= options.MaxAge.Value);
        }

        return await query
            .OrderBy(p => p.Player.PlayerId)
            .Select(p => new MarketItemGenerationCandidate(
                p.Player.PlayerId,
                p.Player.Name,
                p.Player.PositionId,
                p.Player.Position.Name,
                p.Player.Overall,
                p.Player.Age,
                p.Team != null ? p.Team.TeamId : null,
                p.Team != null ? p.Team.TeamName : null))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    private static List<MarketItemGenerationCandidate> SelectCandidates(
        List<MarketItemGenerationCandidate> eligible,
        MarketItemGenerationOptions options,
        int seed)
    {
        var rng = new Random(seed);
        var pool = eligible.OrderBy(_ => rng.NextDouble()).ToList();
        var selected = new List<MarketItemGenerationCandidate>(options.DesiredCount);
        var perTeam = new Dictionary<Guid, int>();
        var limit = options.MaxPerTeam.GetValueOrDefault();
        var hasLimit = options.MaxPerTeam.HasValue && options.MaxPerTeam.Value > 0;
        var perPosition = new Dictionary<short, int>();
        var positionLimit = options.MaxPerPosition.GetValueOrDefault();
        var hasPositionLimit = options.MaxPerPosition.HasValue && options.MaxPerPosition.Value > 0;

        foreach (var candidate in pool)
        {
            if (selected.Count >= options.DesiredCount)
            {
                break;
            }

            if (hasLimit && candidate.TeamId.HasValue)
            {
                var teamId = candidate.TeamId.Value;
                perTeam.TryGetValue(teamId, out var count);
                if (count >= limit)
                {
                    continue;
                }

                perTeam[teamId] = count + 1;
            }

            if (hasPositionLimit)
            {
                perPosition.TryGetValue(candidate.PositionId, out var positionCount);
                if (positionCount >= positionLimit)
                {
                    continue;
                }

                perPosition[candidate.PositionId] = positionCount + 1;
            }

            selected.Add(candidate);
        }

        return selected;
    }

    private static IReadOnlyDictionary<int, decimal> CalculateScarcityMultipliers(
        IReadOnlyList<MarketItemGenerationCandidate> pool,
        IReadOnlyList<MarketItemGenerationCandidate> selected)
    {
        var result = new Dictionary<int, decimal>(selected.Count);
        if (selected.Count == 0)
        {
            return result;
        }

        var groupedByPosition = pool
            .GroupBy(player => player.PositionId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(candidate => candidate.Overall)
                    .OrderBy(value => value)
                    .ToArray());

        var scarcity = new List<(int PlayerId, int Count)>(selected.Count);
        foreach (var candidate in selected)
        {
            if (!groupedByPosition.TryGetValue(candidate.PositionId, out var overalls) || overalls.Length == 0)
            {
                scarcity.Add((candidate.PlayerId, 0));
                continue;
            }

            var count = CountGreaterOrEqual(overalls, candidate.Overall);
            scarcity.Add((candidate.PlayerId, count));
        }

        var multipliers = new[] { 1.5m, 1.4m, 1.3m, 1.1m, 1.0m, 0.9m };
        var ordered = scarcity
            .OrderBy(entry => entry.Count)
            .ThenBy(entry => entry.PlayerId)
            .ToList();

        for (var i = 0; i < ordered.Count; i++)
        {
            var multiplier = i < multipliers.Length ? multipliers[i] : multipliers[^1];
            result[ordered[i].PlayerId] = multiplier;
        }

        return result;
    }

    private static int CountGreaterOrEqual(int[] sortedValues, int threshold)
    {
        if (sortedValues.Length == 0)
        {
            return 0;
        }

        var index = LowerBound(sortedValues, threshold);
        return sortedValues.Length - index;
    }

    private static int LowerBound(int[] values, int threshold)
    {
        var left = 0;
        var right = values.Length;
        while (left < right)
        {
            var mid = left + ((right - left) / 2);
            if (values[mid] < threshold)
            {
                left = mid + 1;
            }
            else
            {
                right = mid;
            }
        }

        return left;
    }

    private IReadOnlyList<DateTime> BuildExpirationSchedule(int count, MarketCycle cycle, MarketItemGenerationOptions options, int seed)
    {
        if (count == 0)
        {
            return Array.Empty<DateTime>();
        }

        var start = EnsureUtc(cycle.StartsAtUtc);
        var end = EnsureUtc(cycle.EndsAtUtc);
        if (end <= start)
        {
            throw new MarketValidationException("A data de término do ciclo deve ser posterior ao início.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var effectiveStart = now > start ? now : start;
        var effectiveEnd = end;

        if (options.AutoSpreadExpirationsAcrossCycle)
        {
            var totalSeconds = (effectiveEnd - effectiveStart).TotalSeconds;
            if (totalSeconds <= count)
            {
                throw new MarketValidationException("Não há janela suficiente no ciclo para distribuir as expirações.");
            }

            var interval = totalSeconds / (count + 1);
            var schedule = new List<DateTime>(count);
            for (var i = 0; i < count; i++)
            {
                var seconds = interval * (i + 1);
                var expiration = effectiveStart.AddSeconds(seconds);
                schedule.Add(ClampExpiration(expiration, effectiveStart, effectiveEnd));
            }
            return schedule;
        }

        if (!options.MinItemLifespan.HasValue || !options.MaxItemLifespan.HasValue)
        {
            throw new MarketValidationException("Informe as durações mínima e máxima para o modo manual.");
        }

        var min = options.MinItemLifespan.Value;
        var max = options.MaxItemLifespan.Value;

        if (min <= TimeSpan.Zero || max <= TimeSpan.Zero)
        {
            throw new MarketValidationException("As durações devem ser positivas.");
        }

        if (max < min)
        {
            throw new MarketValidationException("A duração máxima deve ser maior ou igual à mínima.");
        }

        var random = new Random(seed);
        var expirations = new List<DateTime>(count);
        for (var i = 0; i < count; i++)
        {
            var spanSeconds = max == min
                ? min.TotalSeconds
                : min.TotalSeconds + random.NextDouble() * (max - min).TotalSeconds;
            var expiration = effectiveStart.AddSeconds(spanSeconds);
            expirations.Add(ClampExpiration(expiration, effectiveStart, effectiveEnd));
        }

        expirations.Sort();
        return expirations;
    }

    private async Task<IReadOnlyList<MarketItemGenerationItem>> BuildItemsAsync(GenerationContext context, CancellationToken ct)
    {
        var items = new List<MarketItemGenerationItem>(context.SelectedPlayers.Count);
        foreach (var selected in context.SelectedPlayers)
        {
            var (pricing, age) = await CalculatePricingAsync(selected, ct).ConfigureAwait(false);
            items.Add(new MarketItemGenerationItem(
                selected.Candidate.PlayerId,
                selected.Candidate.PlayerName,
                selected.Candidate.PositionId,
                selected.Candidate.PositionName,
                selected.Candidate.Overall,
                selected.Candidate.Age ?? age,
                selected.Candidate.TeamId,
                selected.Candidate.TeamName,
                pricing.BasePrice,
                pricing.BuyNowPrice,
                pricing.MinIncrement,
                selected.ExpiresAtUtc));
        }

        return items;
    }

    private async Task<(PricingResult Pricing, int Age)> CalculatePricingAsync(SelectedCandidate selected, CancellationToken ct)
    {
        var age = selected.Candidate.Age;
        if (!age.HasValue)
        {
            age = await _dbContext.Players
                .AsNoTracking()
                .Where(player => player.PlayerId == selected.Candidate.PlayerId)
                .Select(player => player.Age)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        if (!age.HasValue)
        {
            throw new InvalidOperationException($"Jogador {selected.Candidate.PlayerName} não possui idade cadastrada.");
        }

        var weight = MarketWeightResolver.GetByPositionId(selected.Candidate.PositionId);
        var multiplier = selected.PriceMultiplier <= 0 ? 1m : selected.PriceMultiplier;
        var pricing = _pricingService.Calculate(weight * multiplier, selected.Candidate.Overall, age.Value);
        return (pricing, age.Value);
    }

    private static int CalculateMaxSelectable(
        IReadOnlyList<MarketItemGenerationCandidate> eligible,
        int? maxPerTeam,
        int? maxPerPosition)
    {
        var total = eligible.Count;

        if (maxPerTeam.HasValue && maxPerTeam.Value > 0)
        {
            var limit = maxPerTeam.Value;
            var teamTotal = 0;
            foreach (var group in eligible.GroupBy(p => p.TeamId))
            {
                if (group.Key.HasValue)
                {
                    teamTotal += Math.Min(group.Count(), limit);
                }
                else
                {
                    teamTotal += group.Count();
                }
            }

            total = Math.Min(total, teamTotal);
        }

        if (maxPerPosition.HasValue && maxPerPosition.Value > 0)
        {
            var positionLimit = maxPerPosition.Value;
            var positionTotal = eligible
                .GroupBy(p => p.PositionId)
                .Sum(group => Math.Min(group.Count(), positionLimit));

            total = Math.Min(total, positionTotal);
        }

        return total;
    }

    private static DateTime ClampExpiration(DateTime expiration, DateTime start, DateTime end)
    {
        var adjusted = expiration <= start ? start.AddMinutes(5) : expiration;
        if (adjusted >= end)
        {
            adjusted = end.AddSeconds(-1);
        }

        return adjusted;
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private sealed record SelectedCandidate(
        MarketItemGenerationCandidate Candidate,
        DateTime ExpiresAtUtc,
        decimal PriceMultiplier);

    private sealed record GenerationContext(
        Guid CycleId,
        int Seed,
        int EligibleCount,
        IReadOnlyList<SelectedCandidate> SelectedPlayers,
        DateTime? FirstExpirationUtc,
        DateTime? LastExpirationUtc);
}
