using System.Collections.Generic;
using System.Data;
using System.Linq;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
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
        var items = await BuildItemsAsync(context.SelectedPlayers, context.ExpirationSchedule, ct).ConfigureAwait(false);

        return new MarketItemGenerationPreview(
            context.Cycle.CycleId,
            context.RequestedCount,
            context.EligibleCount,
            items.Count,
            context.SkippedByLimits,
            context.Seed,
            items.Count == 0 ? null : items.Min(i => i.ExpiresAtUtc),
            items.Count == 0 ? null : items.Max(i => i.ExpiresAtUtc),
            items);
    }

    public async Task<MarketItemGenerationResult> GenerateAsync(Guid cycleId, MarketItemGenerationOptions options, CancellationToken ct)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                var context = await PrepareAsync(cycleId, options, ct).ConfigureAwait(false);
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                var existingPlayers = await _dbContext.MarketItems
                    .Where(i => i.CycleId == cycleId)
                    .Select(i => i.PlayerId)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var existingSet = new HashSet<int>(existingPlayers);
                var createdItems = new List<MarketItemGenerationItem>();
                var skipped = context.SkippedByLimits;

                var expirationEnumerator = context.ExpirationSchedule.GetEnumerator();
                foreach (var player in context.SelectedPlayers)
                {
                    if (!expirationEnumerator.MoveNext())
                    {
                        break;
                    }

                    if (existingSet.Contains(player.PlayerId))
                    {
                        skipped++;
                        continue;
                    }

                    var pricing = await _pricingService.CalculateForPlayerAsync(player.PlayerId, ct).ConfigureAwait(false);

                    var item = new MarketItem
                    {
                        ItemId = Guid.NewGuid(),
                        CycleId = cycleId,
                        PlayerId = player.PlayerId,
                        BasePrice = pricing.BasePrice,
                        BuyNowPrice = pricing.BuyNowPrice,
                        MinIncrement = pricing.MinIncrement,
                        ExpiresAtUtc = expirationEnumerator.Current,
                        Status = MarketItemStatus.Draft,
                        CreatedAtUtc = now,
                        LastUpdateUtc = now
                    };

                    await _dbContext.MarketItems.AddAsync(item, ct).ConfigureAwait(false);
                    existingSet.Add(player.PlayerId);

                    createdItems.Add(new MarketItemGenerationItem(
                        player.PlayerId,
                        player.PlayerName,
                        player.PositionId,
                        player.PositionName,
                        player.Overall,
                        player.Age,
                        pricing.BasePrice,
                        pricing.BuyNowPrice,
                        pricing.MinIncrement,
                        expirationEnumerator.Current));
                }

                if (createdItems.Count > 0)
                {
                    await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                }

                await tx.CommitAsync(ct).ConfigureAwait(false);

                return new MarketItemGenerationResult(
                    context.Cycle.CycleId,
                    context.RequestedCount,
                    context.EligibleCount,
                    createdItems.Count,
                    skipped,
                    context.Seed,
                    createdItems.Count == 0 ? null : createdItems.Min(i => i.ExpiresAtUtc),
                    createdItems.Count == 0 ? null : createdItems.Max(i => i.ExpiresAtUtc),
                    createdItems);
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
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var filters = options.Filters ?? new MarketItemGenerationFilters(null, null, null);
        var expirationOptions = options.ExpirationOptions ?? new MarketItemExpirationOptions(true, null, null);

        if (options.DesiredCount.HasValue && options.DesiredCount.Value <= 0)
        {
            throw new MarketValidationException("Informe uma quantidade desejada maior que zero.");
        }

        if (options.MaxPerTeam.HasValue && options.MaxPerTeam.Value <= 0)
        {
            throw new MarketValidationException("O limite por time deve ser maior que zero.");
        }

        if (filters.MinOverall.HasValue && filters.MaxOverall.HasValue && filters.MinOverall > filters.MaxOverall)
        {
            throw new MarketValidationException("O overall mínimo deve ser menor ou igual ao máximo.");
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

        var duration = cycle.EndsAtUtc - cycle.StartsAtUtc;
        if (duration <= TimeSpan.Zero)
        {
            throw new MarketValidationException("O término do ciclo deve ser posterior ao início.");
        }

        if (!expirationOptions.AutoSpreadAcrossCycle)
        {
            if (!expirationOptions.MinItemLifespan.HasValue || !expirationOptions.MaxItemLifespan.HasValue)
            {
                throw new MarketValidationException("Informe as durações mínima e máxima para expiração manual.");
            }

            if (expirationOptions.MinItemLifespan.Value <= TimeSpan.Zero || expirationOptions.MaxItemLifespan.Value <= TimeSpan.Zero)
            {
                throw new MarketValidationException("As durações devem ser positivas.");
            }

            if (expirationOptions.MinItemLifespan > expirationOptions.MaxItemLifespan)
            {
                throw new MarketValidationException("A duração mínima deve ser menor ou igual à máxima.");
            }
        }

        var existingPlayers = await _dbContext.MarketItems
            .AsNoTracking()
            .Where(i => i.CycleId == cycleId)
            .Select(i => i.PlayerId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var excludedPlayers = new HashSet<int>();
        if (options.EnsureUniquePlayerPerCycle)
        {
            foreach (var playerId in existingPlayers)
            {
                excludedPlayers.Add(playerId);
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
                excludedPlayers.Add(playerId);
            }
        }

        var positionFilter = filters.PositionIds is { Count: > 0 }
            ? filters.PositionIds.Distinct().ToHashSet()
            : null;

        var eligibleQuery = _dbContext.TeamRosters
            .AsNoTracking()
            .Select(r => new
            {
                r.TeamId,
                r.Player.PlayerId,
                r.Player.Name,
                r.Player.PositionId,
                PositionName = r.Player.Position.Name,
                r.Player.Overall,
                r.Player.Age
            })
            .Where(r => r.Age.HasValue);

        if (positionFilter is not null)
        {
            eligibleQuery = eligibleQuery.Where(r => positionFilter.Contains(r.PositionId));
        }

        if (filters.MinOverall.HasValue)
        {
            eligibleQuery = eligibleQuery.Where(r => r.Overall >= filters.MinOverall.Value);
        }

        if (filters.MaxOverall.HasValue)
        {
            eligibleQuery = eligibleQuery.Where(r => r.Overall <= filters.MaxOverall.Value);
        }

        if (excludedPlayers.Count > 0)
        {
            eligibleQuery = eligibleQuery.Where(r => !excludedPlayers.Contains(r.PlayerId));
        }

        var eligiblePlayers = await eligibleQuery
            .OrderBy(r => r.PlayerId)
            .Select(r => new EligiblePlayer(
                r.PlayerId,
                r.Name,
                r.PositionId,
                r.PositionName,
                r.Overall,
                r.Age!.Value,
                r.TeamId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var eligibleCount = eligiblePlayers.Count;
        var requestedCount = options.DesiredCount.HasValue
            ? Math.Max(0, options.DesiredCount.Value)
            : eligibleCount;

        var selectionTarget = Math.Min(requestedCount, eligibleCount);
        var seed = options.Seed ?? Random.Shared.Next();
        var selectedPlayers = SelectPlayers(eligiblePlayers, selectionTarget, seed, options.MaxPerTeam);
        var skippedByLimits = Math.Max(0, requestedCount - selectedPlayers.Count);
        var expirationSchedule = ComputeExpirations(cycle, selectedPlayers.Count, seed, expirationOptions);

        return new GenerationContext(
            cycle,
            seed,
            requestedCount,
            eligibleCount,
            skippedByLimits,
            selectedPlayers,
            expirationSchedule);
    }

    private List<SelectedPlayer> SelectPlayers(
        IReadOnlyList<EligiblePlayer> eligible,
        int requested,
        int seed,
        int? maxPerTeam)
    {
        if (requested <= 0 || eligible.Count == 0)
        {
            return new List<SelectedPlayer>();
        }

        var rng = new Random(seed);
        var shuffled = eligible
            .OrderBy(_ => rng.Next())
            .ToList();

        var teamLimits = new Dictionary<Guid, int>();
        var result = new List<SelectedPlayer>(requested);
        var limit = maxPerTeam.GetValueOrDefault(int.MaxValue);

        foreach (var player in shuffled)
        {
            if (result.Count >= requested)
            {
                break;
            }

            if (limit < int.MaxValue)
            {
                teamLimits.TryGetValue(player.TeamId, out var count);
                if (count >= limit)
                {
                    continue;
                }

                teamLimits[player.TeamId] = count + 1;
            }

            result.Add(new SelectedPlayer(
                player.PlayerId,
                player.PlayerName,
                player.PositionId,
                player.PositionName,
                player.Overall,
                player.Age,
                player.TeamId));
        }

        return result;
    }

    private async Task<IReadOnlyList<MarketItemGenerationItem>> BuildItemsAsync(
        IReadOnlyList<SelectedPlayer> players,
        IReadOnlyList<DateTime> expirations,
        CancellationToken ct)
    {
        var items = new List<MarketItemGenerationItem>(players.Count);

        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            var expiresAt = expirations.Count > i ? expirations[i] : expirations.LastOrDefault();
            var pricing = await _pricingService.CalculateForPlayerAsync(player.PlayerId, ct).ConfigureAwait(false);

            items.Add(new MarketItemGenerationItem(
                player.PlayerId,
                player.PlayerName,
                player.PositionId,
                player.PositionName,
                player.Overall,
                player.Age,
                pricing.BasePrice,
                pricing.BuyNowPrice,
                pricing.MinIncrement,
                expiresAt));
        }

        return items;
    }

    private static IReadOnlyList<DateTime> ComputeExpirations(
        MarketCycle cycle,
        int count,
        int seed,
        MarketItemExpirationOptions options)
    {
        var expirations = new List<DateTime>(count);
        if (count == 0)
        {
            return expirations;
        }

        if (options.AutoSpreadAcrossCycle)
        {
            var duration = cycle.EndsAtUtc - cycle.StartsAtUtc;
            if (duration <= TimeSpan.Zero)
            {
                return Enumerable.Repeat(cycle.EndsAtUtc, count).ToList();
            }

            if (count == 1)
            {
                expirations.Add(cycle.StartsAtUtc + TimeSpan.FromTicks(duration.Ticks / 2));
                return expirations;
            }

            var step = duration.TotalSeconds / (count + 1);
            for (var i = 0; i < count; i++)
            {
                var seconds = step * (i + 1);
                var expiresAt = cycle.StartsAtUtc.AddSeconds(seconds);
                if (expiresAt > cycle.EndsAtUtc)
                {
                    expiresAt = cycle.EndsAtUtc;
                }

                expirations.Add(expiresAt);
            }

            return expirations;
        }

        var min = options.MinItemLifespan!.Value;
        var max = options.MaxItemLifespan!.Value;
        var rng = new Random(HashCode.Combine(seed, 0x9E3779B9));

        for (var i = 0; i < count; i++)
        {
            var rangeSeconds = max.TotalSeconds - min.TotalSeconds;
            var offset = rangeSeconds <= 0
                ? 0
                : rng.NextDouble() * rangeSeconds;
            var lifespan = min.TotalSeconds + offset;
            var expiresAt = cycle.StartsAtUtc.AddSeconds(lifespan);
            if (expiresAt > cycle.EndsAtUtc)
            {
                expiresAt = cycle.EndsAtUtc;
            }

            expirations.Add(expiresAt);
        }

        return expirations;
    }

    private sealed record EligiblePlayer(
        int PlayerId,
        string PlayerName,
        short PositionId,
        string PositionName,
        int Overall,
        int Age,
        Guid TeamId);

    private sealed record SelectedPlayer(
        int PlayerId,
        string PlayerName,
        short PositionId,
        string PositionName,
        int Overall,
        int Age,
        Guid TeamId);

    private sealed record GenerationContext(
        MarketCycle Cycle,
        int Seed,
        int RequestedCount,
        int EligibleCount,
        int SkippedByLimits,
        IReadOnlyList<SelectedPlayer> SelectedPlayers,
        IReadOnlyList<DateTime> ExpirationSchedule);
}
