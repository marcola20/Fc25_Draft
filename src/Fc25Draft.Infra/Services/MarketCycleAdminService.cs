using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class MarketCycleAdminService : IMarketCycleAdminService
{
    private readonly DraftDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public MarketCycleAdminService(DraftDbContext dbContext, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MarketCycleDto> CreateAsync(MarketCycleCreateCommand command, CancellationToken ct)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var normalizedStart = EnsureUtc(command.StartsAtUtc);
        var normalizedEnd = EnsureUtc(command.EndsAtUtc);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new MarketValidationException("O nome do ciclo é obrigatório.");
        }

        if (normalizedStart >= normalizedEnd)
        {
            throw new MarketValidationException("A data de início deve ser anterior à data de término.");
        }

        if (command.Status == MarketCycleStatus.Open)
        {
            var hasOpenCycle = await _dbContext.MarketCycles
                .AsNoTracking()
                .AnyAsync(c => c.Status == MarketCycleStatus.Open, ct)
                .ConfigureAwait(false);

            if (hasOpenCycle)
            {
                throw new MarketConflictException("Já existe um ciclo aberto.");
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var cycle = new MarketCycle
        {
            CycleId = Guid.NewGuid(),
            Name = command.Name.Trim(),
            Status = command.Status,
            StartsAtUtc = normalizedStart,
            EndsAtUtc = normalizedEnd,
            Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _dbContext.MarketCycles.AddAsync(cycle, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return ToDto(cycle);
    }

    public async Task<PagedResult<MarketCycleDto>> QueryAsync(MarketCycleQuery command, CancellationToken ct)
    {
        if (command is null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var page = Math.Max(1, command.Page);
        var pageSize = Math.Clamp(command.PageSize, 1, 100);

        var query = _dbContext.MarketCycles.AsNoTracking().AsQueryable();

        if (command.Status.HasValue)
        {
            query = query.Where(c => c.Status == command.Status.Value);
        }

        if (command.StartsAfterUtc.HasValue)
        {
            var startsAfter = EnsureUtc(command.StartsAfterUtc.Value);
            query = query.Where(c => c.StartsAtUtc >= startsAfter);
        }

        if (command.StartsBeforeUtc.HasValue)
        {
            var startsBefore = EnsureUtc(command.StartsBeforeUtc.Value);
            query = query.Where(c => c.StartsAtUtc <= startsBefore);
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        if (total == 0)
        {
            return PagedResult<MarketCycleDto>.Empty(page, pageSize);
        }

        var items = await query
            .OrderByDescending(c => c.StartsAtUtc)
            .ThenByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new MarketCycleDto(
                c.CycleId,
                c.Name,
                c.Status,
                c.StartsAtUtc,
                c.EndsAtUtc,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                c.Notes))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<MarketCycleDto>(items, total, page, pageSize);
    }

    public async Task<MarketCycleDto?> GetByIdAsync(Guid cycleId, CancellationToken ct)
    {
        var cycle = await _dbContext.MarketCycles
            .AsNoTracking()
            .Where(c => c.CycleId == cycleId)
            .Select(c => new MarketCycleDto(
                c.CycleId,
                c.Name,
                c.Status,
                c.StartsAtUtc,
                c.EndsAtUtc,
                c.CreatedAtUtc,
                c.UpdatedAtUtc,
                c.Notes))
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return cycle;
    }

    public async Task<MarketCycleDto> UpdateStatusAsync(Guid cycleId, MarketCycleStatus status, bool forceClose, CancellationToken ct)
    {
        var cycle = await _dbContext.MarketCycles
            .FirstOrDefaultAsync(c => c.CycleId == cycleId, ct)
            .ConfigureAwait(false);

        if (cycle is null)
        {
            throw new MarketNotFoundException("Ciclo não encontrado.");
        }

        if (cycle.Status == status)
        {
            return ToDto(cycle);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (status == MarketCycleStatus.Open)
        {
            var hasOtherOpen = await _dbContext.MarketCycles
                .AsNoTracking()
                .AnyAsync(c => c.CycleId != cycleId && c.Status == MarketCycleStatus.Open, ct)
                .ConfigureAwait(false);

            if (hasOtherOpen)
            {
                throw new MarketConflictException("Já existe um ciclo aberto.");
            }

            var items = await _dbContext.MarketItems
                .Where(i => i.CycleId == cycleId && i.Status == MarketItemStatus.Draft)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var item in items)
            {
                if (item.ExpiresAtUtc <= now)
                {
                    item.Status = MarketItemStatus.Canceled;
                    item.LastUpdateUtc = now;
                    continue;
                }

                // FIX: Promote draft market items when opening a cycle so they appear in the active market.
                item.Status = MarketItemStatus.Published;
                item.PublishedAtUtc = now;
                item.LastUpdateUtc = now;
            }
        }

        if (status == MarketCycleStatus.Closed)
        {
            var hasActiveItems = await _dbContext.MarketItems
                .AsNoTracking()
                .AnyAsync(i => i.CycleId == cycleId && i.Status == MarketItemStatus.Published, ct)
                .ConfigureAwait(false);

            if (hasActiveItems && !forceClose)
            {
                throw new MarketValidationException("Existem itens ativos neste ciclo. Utilize o fechamento forçado para continuar.");
            }

            if (hasActiveItems && forceClose)
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var items = await _dbContext.MarketItems
                    .Where(i => i.CycleId == cycleId && i.Status == MarketItemStatus.Published)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                foreach (var item in items)
                {
                    item.Status = MarketItemStatus.Canceled;
                    item.LastUpdateUtc = now;
                }
            }
        }

        cycle.Status = status;
        cycle.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return ToDto(cycle);
    }

    private static MarketCycleDto ToDto(MarketCycle cycle) => new(
        cycle.CycleId,
        cycle.Name,
        cycle.Status,
        cycle.StartsAtUtc,
        cycle.EndsAtUtc,
        cycle.CreatedAtUtc,
        cycle.UpdatedAtUtc,
        cycle.Notes);

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }
}
