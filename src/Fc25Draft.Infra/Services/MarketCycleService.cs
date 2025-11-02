using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class MarketCycleService : IMarketCycleService
{
    private readonly DraftDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public MarketCycleService(DraftDbContext dbContext, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MarketCycleDto?> ResolveAsync(Guid? cycleId, CancellationToken ct)
    {
        if (cycleId.HasValue && cycleId.Value != Guid.Empty)
        {
            var requested = await _dbContext.MarketCycles
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CycleId == cycleId.Value, ct)
                .ConfigureAwait(false);

            return requested is null ? null : ToDto(requested);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var active = await _dbContext.MarketCycles
            .AsNoTracking()
            .Where(c => c.Status == MarketCycleStatus.Active)
            .OrderByDescending(c => c.StartsAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (active is not null)
        {
            return ToDto(active);
        }

        var upcoming = await _dbContext.MarketCycles
            .AsNoTracking()
            .Where(c => c.Status == MarketCycleStatus.Draft && c.StartsAtUtc > now)
            .OrderBy(c => c.StartsAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (upcoming is not null)
        {
            return ToDto(upcoming);
        }

        var last = await _dbContext.MarketCycles
            .AsNoTracking()
            .OrderByDescending(c => c.StartsAtUtc)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return last is null ? null : ToDto(last);
    }

    public async Task<IReadOnlyList<MarketCycleDto>> ListActiveAsync(CancellationToken ct)
    {
        var cycles = await _dbContext.MarketCycles
            .AsNoTracking()
            .Where(c => c.Status == MarketCycleStatus.Active)
            .OrderBy(c => c.StartsAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return cycles.Count == 0
            ? Array.Empty<MarketCycleDto>()
            : cycles.Select(ToDto).ToArray();
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
}
