using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Fc25Draft.Infra.Services;

public class MarketCycleAdminService : IMarketCycleAdminService
{
    private readonly DraftDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly IAuctionSettlementService _settlementService;

    public MarketCycleAdminService(
        DraftDbContext dbContext,
        IAuctionSettlementService settlementService,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _settlementService = settlementService ?? throw new ArgumentNullException(nameof(settlementService));
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

        if (command.Status == MarketCycleStatus.Active)
        {
            var hasOpenCycle = await _dbContext.MarketCycles
                .AsNoTracking()
                .AnyAsync(c => c.Status == MarketCycleStatus.Active, ct)
                .ConfigureAwait(false);

            if (hasOpenCycle)
            {
                throw new MarketConflictException("Já existe um ciclo ativo.");
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

    public async Task<MarketCycleDto> UpdateStatusAsync(Guid cycleId, MarketCycleStatus newStatus, bool forceClose, CancellationToken ct)
    {
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database
                .BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var cycle = await _dbContext.MarketCycles
                .SingleOrDefaultAsync(c => c.CycleId == cycleId, ct)
                ?? throw new MarketNotFoundException("Ciclo não encontrado.");

            if (newStatus == MarketCycleStatus.Active)
            {
                if (cycle.Status == MarketCycleStatus.Closed)
                    throw new MarketConflictException("Não é possível reabrir um ciclo encerrado.");

                cycle.Status = MarketCycleStatus.Active;
                cycle.UpdatedAtUtc = nowUtc;

                await _dbContext.MarketItems
                    .Where(i => i.CycleId == cycleId && i.Status == MarketItemStatus.Draft)
                    .ExecuteUpdateAsync(set => set
                        .SetProperty(i => i.Status, MarketItemStatus.Active)
                        .SetProperty(i => i.LastUpdateUtc, nowUtc),
                        ct);
            }
            else if (newStatus == MarketCycleStatus.Closed)
            {
                if (!forceClose && cycle.Status != MarketCycleStatus.Active)
                    throw new MarketConflictException("Apenas ciclos ativos podem ser encerrados sem 'force'.");

                cycle.Status = MarketCycleStatus.Closed;
                cycle.UpdatedAtUtc = nowUtc;
            }
            else
            {
                cycle.Status = newStatus;
                cycle.UpdatedAtUtc = nowUtc;
            }

            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ToDto(cycle);
        });
    }

    public async Task<MarketCycleStatusUpdateResult> ConcludeAsync(Guid cycleId, CancellationToken ct)
    {
        var cycle = await _dbContext.MarketCycles
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CycleId == cycleId, ct)
            .ConfigureAwait(false)
            ?? throw new MarketNotFoundException("Ciclo não encontrado.");

        if (cycle.Status != MarketCycleStatus.Active && cycle.Status != MarketCycleStatus.Closed)
        {
            throw new MarketConflictException("Apenas ciclos ativos podem ser concluídos.");
        }

        if (cycle.Status == MarketCycleStatus.Active)
        {
            await _settlementService
                .SettleAllOpenItemsOnCycleCloseAsync(cycleId, forceClose: false, ct)
                .ConfigureAwait(false);

            var updated = await GetByIdAsync(cycleId, ct).ConfigureAwait(false)
                ?? throw new MarketNotFoundException("Ciclo não encontrado após conclusão.");

            var summary = await LoadSettlementSummaryAsync(cycleId, ct).ConfigureAwait(false);
            return new MarketCycleStatusUpdateResult(updated, summary);
        }

        var existingSummary = await LoadSettlementSummaryAsync(cycleId, ct).ConfigureAwait(false);
        return new MarketCycleStatusUpdateResult(ToDto(cycle), existingSummary);
    }

    private async Task<MarketCycleSettlementSummary> LoadSettlementSummaryAsync(Guid cycleId, CancellationToken ct)
    {
        var sold = await _dbContext.MarketItems
            .AsNoTracking()
            .CountAsync(i => i.CycleId == cycleId && i.Status == MarketItemStatus.Sold, ct)
            .ConfigureAwait(false);

        var expired = await _dbContext.MarketItems
            .AsNoTracking()
            .CountAsync(i => i.CycleId == cycleId && i.Status == MarketItemStatus.Expired, ct)
            .ConfigureAwait(false);

        var canceled = await _dbContext.MarketItems
            .AsNoTracking()
            .CountAsync(i => i.CycleId == cycleId && i.Status == MarketItemStatus.Canceled, ct)
            .ConfigureAwait(false);

        return new MarketCycleSettlementSummary(sold, expired, canceled);
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

    private static DateTime EnsureUtc(DateTime value) =>
    value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
