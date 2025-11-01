using System;
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
        var items = await BuildItemsAsync(context.SelectedPlayers, context.ExpiresAtUtc, ct).ConfigureAwait(false);

        return new MarketItemGenerationPreview(
            context.CycleId,
            options.DesiredCount,
            context.EligibleCount,
            context.Seed,
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
                var context = await PrepareAsync(cycleId, options, ct);

                var existingPlayers = await _dbContext.MarketItems
                    .Where(i => i.CycleId == cycleId)
                    .Select(i => i.PlayerId)
                    .ToListAsync(ct);

                var existingSet = new HashSet<int>(existingPlayers);
                var createdItems = new List<MarketItemGenerationItem>();
                var skipped = 0;
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                foreach (var candidate in context.SelectedPlayers)
                {
                    if (existingSet.Contains(candidate.PlayerId))
                    {
                        skipped++;
                        continue;
                    }

                    var pricing = await _pricingService.CalculateForPlayerAsync(candidate.PlayerId, ct);

                    var item = new MarketItem
                    {
                        ItemId = Guid.NewGuid(),
                        CycleId = cycleId,
                        PlayerId = candidate.PlayerId,
                        BasePrice = pricing.BasePrice,
                        BuyNowPrice = pricing.BuyNowPrice,
                        MinIncrement = pricing.MinIncrement,
                        ExpiresAtUtc = context.ExpiresAtUtc,
                        Status = MarketItemStatus.Draft,
                        CreatedAtUtc = now,
                        LastUpdateUtc = now
                    };

                    await _dbContext.MarketItems.AddAsync(item, ct);
                    existingSet.Add(candidate.PlayerId);

                    createdItems.Add(new MarketItemGenerationItem(
                        candidate.PlayerId,
                        candidate.PlayerName,
                        candidate.PositionId,
                        candidate.PositionName,
                        candidate.Overall,
                        candidate.Age,
                        pricing.BasePrice,
                        pricing.BuyNowPrice,
                        pricing.MinIncrement,
                        context.ExpiresAtUtc));
                }

                if (createdItems.Count > 0)
                {
                    try
                    {
                        await _dbContext.SaveChangesAsync(ct);
                    }
                    catch (DbUpdateException)
                    {
                        var already = await _dbContext.MarketItems
                            .Where(i => i.CycleId == cycleId)
                            .Select(i => i.PlayerId)
                            .ToListAsync(ct);

                        var alreadySet = new HashSet<int>(already);
                        skipped += createdItems.Count(ci => alreadySet.Contains(ci.PlayerId));
                        createdItems.RemoveAll(ci => alreadySet.Contains(ci.PlayerId));
                    }
                }

                await tx.CommitAsync(ct);

                return new MarketItemGenerationResult(
                    context.CycleId,
                    options.DesiredCount,
                    context.EligibleCount,
                    context.Seed,
                    createdItems.Count,
                    skipped,
                    createdItems);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
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

        if (options.DesiredCount <= 0)
        {
            throw new MarketValidationException("A quantidade desejada deve ser maior que zero.");
        }

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
            throw new MarketValidationException("Itens só podem ser gerados para ciclos em rascunho.");
        }

        var lifecycle = NormalizeLifecycle(options.LifecycleOptions, cycle);
        var excludedPlayers = await LoadExcludedPlayersAsync(cycleId, ct).ConfigureAwait(false);
        var eligiblePlayers = await QueryEligiblePlayersAsync(options.Filters, excludedPlayers, ct).ConfigureAwait(false);

        if (eligiblePlayers.Count == 0)
        {
            throw new MarketValidationException("Nenhum jogador elegível para os filtros informados.");
        }

        if (options.DesiredCount > eligiblePlayers.Count)
        {
            throw new MarketValidationException($"A quantidade desejada ({options.DesiredCount}) é maior do que o total elegível ({eligiblePlayers.Count}).");
        }

        var seed = options.Seed ?? Random.Shared.Next();
        var selected = SelectPlayers(eligiblePlayers, options.DesiredCount, seed);

        return new GenerationContext(
            cycle.CycleId,
            seed,
            lifecycle.PublishAtUtc,
            lifecycle.ExpiresAtUtc,
            eligiblePlayers.Count,
            selected);
    }

    private async Task<HashSet<int>> LoadExcludedPlayersAsync(Guid cycleId, CancellationToken ct)
    {
        var excluded = await _dbContext.MarketItems
            .AsNoTracking()
            .Where(i => i.CycleId == cycleId)
            .Select(i => i.PlayerId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var openCyclePlayers = await _dbContext.MarketItems
            .AsNoTracking()
            .Where(i => i.Cycle.Status == MarketCycleStatus.Active && i.Status == MarketItemStatus.Active)
            .Select(i => i.PlayerId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var result = new HashSet<int>(excluded);
        foreach (var playerId in openCyclePlayers)
        {
            result.Add(playerId);
        }

        return result;
    }

    private async Task<List<MarketItemGenerationCandidate>> QueryEligiblePlayersAsync(
        MarketItemGenerationFilters filters,
        HashSet<int> excluded,
        CancellationToken ct)
    {
        filters ??= new MarketItemGenerationFilters(null, null, null, null, null, null);

        var excludedIds = excluded.Count > 0 ? excluded.ToArray() : Array.Empty<int>();

        var query = _dbContext.Players
            .AsNoTracking()
            .Where(p => p.CurrentTeamId == null)
            .Where(p => !_dbContext.TeamRosters.Any(r => r.PlayerId == p.PlayerId))
            .Where(p => !_dbContext.MarketItems
                .Any(i => i.PlayerId == p.PlayerId && i.Status == MarketItemStatus.Active && i.Cycle.Status == MarketCycleStatus.Active));

        if (excludedIds.Length > 0)
        {
            query = query.Where(p => !excludedIds.Contains(p.PlayerId));
        }

        if (filters.PlayerIds is { Count: > 0 })
        {
            var ids = filters.PlayerIds.Distinct().ToArray();
            query = query.Where(p => ids.Contains(p.PlayerId));
        }

        if (filters.PositionIds is { Count: > 0 })
        {
            var positions = filters.PositionIds.Distinct().ToArray();
            query = query.Where(p => positions.Contains(p.PositionId));
        }

        if (filters.MinOverall.HasValue)
        {
            query = query.Where(p => p.Overall >= filters.MinOverall.Value);
        }

        if (filters.MaxOverall.HasValue)
        {
            query = query.Where(p => p.Overall <= filters.MaxOverall.Value);
        }

        if (filters.MinAge.HasValue)
        {
            query = query.Where(p => p.Age.HasValue && p.Age.Value >= filters.MinAge.Value);
        }

        if (filters.MaxAge.HasValue)
        {
            query = query.Where(p => p.Age.HasValue && p.Age.Value <= filters.MaxAge.Value);
        }

        return await query
            .OrderBy(p => p.PlayerId)
            .Select(p => new MarketItemGenerationCandidate(
                p.PlayerId,
                p.Name,
                p.PositionId,
                p.Position.Name,
                p.Overall,
                p.Age))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<MarketItemGenerationItem>> BuildItemsAsync(
        IReadOnlyList<MarketItemGenerationCandidate> selected,
        DateTime expiresAtUtc,
        CancellationToken ct)
    {
        var items = new List<MarketItemGenerationItem>(selected.Count);
        foreach (var candidate in selected)
        {
            var pricing = await _pricingService
                .CalculateForPlayerAsync(candidate.PlayerId, ct)
                .ConfigureAwait(false);

            items.Add(new MarketItemGenerationItem(
                candidate.PlayerId,
                candidate.PlayerName,
                candidate.PositionId,
                candidate.PositionName,
                candidate.Overall,
                candidate.Age,
                pricing.BasePrice,
                pricing.BuyNowPrice,
                pricing.MinIncrement,
                expiresAtUtc));
        }

        return items;
    }

    private static IReadOnlyList<MarketItemGenerationCandidate> SelectPlayers(
        IReadOnlyList<MarketItemGenerationCandidate> source,
        int desiredCount,
        int seed)
    {
        var rng = new Random(seed);
        var pool = source.ToList();
        var selected = new List<MarketItemGenerationCandidate>(desiredCount);

        for (var i = 0; i < desiredCount; i++)
        {
            var index = rng.Next(pool.Count);
            selected.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return selected;
    }

    private static NormalizedLifecycle NormalizeLifecycle(MarketItemLifecycleOptions lifecycle, MarketCycle cycle)
    {
        lifecycle ??= new MarketItemLifecycleOptions(null, null, null);

        if (lifecycle.DurationHours.HasValue && lifecycle.DurationHours.Value <= 0)
        {
            throw new MarketValidationException("A duração deve ser maior que zero.");
        }

        var publishAt = lifecycle.PublishAtUtc.HasValue
            ? EnsureUtc(lifecycle.PublishAtUtc.Value)
            : (DateTime?)null;

        DateTime expiresAt;

        if (lifecycle.ExpiresAtUtc.HasValue)
        {
            expiresAt = EnsureUtc(lifecycle.ExpiresAtUtc.Value);
        }
        else if (lifecycle.DurationHours.HasValue)
        {
            var baseDate = publishAt ?? cycle.StartsAtUtc;
            expiresAt = EnsureUtc(baseDate).AddHours(lifecycle.DurationHours.Value);
        }
        else
        {
            expiresAt = cycle.EndsAtUtc;
        }

        if (expiresAt <= cycle.StartsAtUtc)
        {
            throw new MarketValidationException("A data de expiração deve ser posterior ao início do ciclo.");
        }

        return new NormalizedLifecycle(publishAt, expiresAt);
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private sealed record GenerationContext(
        Guid CycleId,
        int Seed,
        DateTime? PublishAtUtc,
        DateTime ExpiresAtUtc,
        int EligibleCount,
        IReadOnlyList<MarketItemGenerationCandidate> SelectedPlayers);

    private sealed record NormalizedLifecycle(DateTime? PublishAtUtc, DateTime ExpiresAtUtc);
}
