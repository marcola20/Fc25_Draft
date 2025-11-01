using System.Security.Cryptography;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Options;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Infra.Services;

public class MarketCycleGenerator : IMarketCycleGenerator
{
    private readonly DraftDbContext _dbContext;
    private readonly IPricingService _pricingService;
    private readonly MarketOptions _marketOptions;
    private readonly TimeProvider _timeProvider;

    public MarketCycleGenerator(
        DraftDbContext dbContext,
        IPricingService pricingService,
        IOptions<MarketOptions> options,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
        _marketOptions = options.Value ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<bool> NeedsNewCycleAsync(DateTime utcNow, CancellationToken ct)
    {
        var openCycleExists = await _dbContext.MarketCycles
            .AsNoTracking()
            .AnyAsync(c => c.Status == MarketCycleStatus.Open, ct)
            .ConfigureAwait(false);

        if (openCycleExists)
        {
            return false;
        }

        var futureDraftExists = await _dbContext.MarketCycles
            .AsNoTracking()
            .AnyAsync(c => c.Status == MarketCycleStatus.Draft && c.StartsAtUtc > utcNow, ct)
            .ConfigureAwait(false);

        if (futureDraftExists)
        {
            return false;
        }

        var lastCycle = await _dbContext.MarketCycles
            .AsNoTracking()
            .OrderByDescending(c => c.StartsAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (lastCycle is null)
        {
            return true;
        }

        return utcNow >= lastCycle.EndsAtUtc;
    }

    public async Task<MarketCycleDto> CreateNewCycleAsync(DateTime utcNow, CancellationToken ct)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var now = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);

            await using var tx = await _dbContext.Database
                .BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);

            try
            {
                var existingOpen = await _dbContext.MarketCycles
                    .FirstOrDefaultAsync(c => c.Status == MarketCycleStatus.Open, ct);

                if (existingOpen is not null)
                    return ToDto(existingOpen);

                var cycleId = Guid.NewGuid();

                var endsAt = now.AddHours(_marketOptions.CycleDurationHours);
                var cycle = new MarketCycle
                {
                    CycleId = cycleId,
                    Name = FormattableString.Invariant($"Ciclo de Mercado {now:yyyyMMddHHmm}"),
                    Status = MarketCycleStatus.Open,
                    StartsAtUtc = now,
                    EndsAtUtc = endsAt,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                var excludedPlayerIds = await LoadExcludedPlayersAsync(ct);
                var selectedPlayers = await SelectPlayersAsync(excludedPlayerIds, ct);

                if (selectedPlayers.Count != _marketOptions.Bands.CountTotal)
                    throw new MarketValidationException(
                        $"Configuração inconsistente: esperado {_marketOptions.Bands.CountTotal} jogadores, obtidos {selectedPlayers.Count}.");

                foreach (var player in selectedPlayers)
                {
                    var age = player.Age ?? 27;
                    var weight = MarketWeightResolver.GetByPositionId(player.PositionId);
                    var varianceFactor = 1m + GetVarianceFactor(player.PlayerId, cycle.CycleId);
                    var pricing = _pricingService.Calculate(weight * varianceFactor, player.Overall, age);

                    cycle.Items.Add(new MarketItem
                    {
                        ItemId = Guid.NewGuid(),
                        CycleId = cycle.CycleId,
                        PlayerId = player.PlayerId,
                        BasePrice = pricing.BasePrice,
                        BuyNowPrice = pricing.BuyNowPrice,
                        MinIncrement = pricing.MinIncrement,
                        Status = MarketItemStatus.Published,
                        PublishedAtUtc = now,
                        ExpiresAtUtc = endsAt,
                        CreatedAtUtc = now,
                        LastUpdateUtc = now
                    });
                }

                await _dbContext.MarketCycles.AddAsync(cycle, ct);
                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                return ToDto(cycle);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        });
    }

    private static MarketCycleDto ToDto(MarketCycle cycle)
    {
        return new MarketCycleDto(
            cycle.CycleId,
            cycle.Name,
            cycle.Status,
            cycle.StartsAtUtc,
            cycle.EndsAtUtc,
            cycle.CreatedAtUtc,
            cycle.UpdatedAtUtc,
            cycle.Notes);
    }

    private async Task<HashSet<int>> LoadExcludedPlayersAsync(CancellationToken ct)
    {
        var ids = await _dbContext.MarketItems
            .AsNoTracking()
            .Where(i => i.Status == MarketItemStatus.Published)
            .Select(i => i.PlayerId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new HashSet<int>(ids);
    }

    private async Task<List<Player>> SelectPlayersAsync(HashSet<int> excluded, CancellationToken ct)
    {
        var total = _marketOptions.Bands.CountTotal;
        var selected = new List<Player>(total);

        await FillBandAsync(_marketOptions.Bands.BandA_Count, _marketOptions.OvrBandA_Min, _marketOptions.OvrBandA_Max, selected, excluded, ct);
        await FillBandAsync(_marketOptions.Bands.BandB_Count, _marketOptions.OvrBandB_Min, _marketOptions.OvrBandB_Max, selected, excluded, ct);
        await FillBandAsync(_marketOptions.Bands.BandC_Count, _marketOptions.OvrBandC_Min, _marketOptions.OvrBandC_Max, selected, excluded, ct, weighted: true);

        if (selected.Count > total)
        {
            selected = selected.Take(total).ToList();
        }

        return selected;
    }

    private async Task FillBandAsync(
        int count,
        int minOvr,
        int maxOvr,
        List<Player> selected,
        HashSet<int> excluded,
        CancellationToken ct,
        bool weighted = false)
    {
        if (count <= 0)
        {
            return;
        }

        var candidates = await _dbContext.Players
            .AsNoTracking()
            .Include(p => p.Position)
            .Where(p => p.CurrentTeamId == null)
            .Where(p => p.Overall >= minOvr && p.Overall <= maxOvr)
            .Where(p => !excluded.Contains(p.PlayerId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (candidates.Count < count)
        {
            throw new MarketValidationException($"Jogadores insuficientes na faixa {minOvr}-{maxOvr} (disponíveis: {candidates.Count}, necessários: {count}).");
        }

        for (var i = 0; i < count; i++)
        {
            var player = weighted
                ? PickWeightedPlayer(candidates)
                : PickRandomPlayer(candidates);

            selected.Add(player);
            excluded.Add(player.PlayerId);
            candidates.Remove(player);
        }
    }

    private static Player PickRandomPlayer(IList<Player> candidates)
    {
        var index = RandomNumberGenerator.GetInt32(candidates.Count);
        return candidates[index];
    }

    private static Player PickWeightedPlayer(IList<Player> candidates)
    {
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        var weights = new double[candidates.Count];
        double totalWeight = 0d;
        for (var i = 0; i < candidates.Count; i++)
        {
            var weight = 1d / Math.Max(1, candidates[i].Overall - 76);
            weights[i] = weight;
            totalWeight += weight;
        }

        var random = RandomNumberGenerator.GetInt32(int.MaxValue) / (double)int.MaxValue;
        double cumulative = 0d;
        for (var i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i] / totalWeight;
            if (random <= cumulative)
            {
                return candidates[i];
            }
        }

        return candidates[^1];
    }

    private decimal GetVarianceFactor(int playerId, Guid cycleId)
    {
        var seed = HashCode.Combine(playerId, cycleId);
        var random = new Random(seed);
        var range = (double)_marketOptions.MarketVariancePct;
        var value = random.NextDouble();
        var adjusted = (value * 2d - 1d) * range;
        return (decimal)adjusted;
    }
}
