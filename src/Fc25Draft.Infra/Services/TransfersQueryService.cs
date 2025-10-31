using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Extensions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class TransfersQueryService : ITransfersQueryService
{
    private readonly DraftDbContext _dbContext;

    public TransfersQueryService(DraftDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<TransferHistoryDto>> QueryHistoryAsync(TransfersFilter filter, CancellationToken ct)
    {
        if (filter is null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        if (filter.Page < 1)
        {
            throw new ArgumentException("Página deve ser maior ou igual a 1.", nameof(filter));
        }

        if (filter.PageSize < 1)
        {
            throw new ArgumentException("Tamanho da página deve ser maior ou igual a 1.", nameof(filter));
        }

        var size = Math.Min(filter.PageSize, 100);
        var query = _dbContext.TransferHistories
            .AsNoTracking()
            .AsQueryable();

        if (filter.TeamId.HasValue)
        {
            var teamId = filter.TeamId.Value;
            query = query.Where(h => h.FromTeamId == teamId || h.ToTeamId == teamId);
        }

        if (filter.PlayerId.HasValue)
        {
            var playerId = filter.PlayerId.Value;
            query = query.Where(h => h.Player.PlayerGuid == playerId);
        }

        if (filter.Type.HasValue)
        {
            var type = filter.Type.Value;
            query = query.Where(h => h.Type == type);
        }

        if (filter.FromUtc.HasValue)
        {
            var from = EnsureUtc(filter.FromUtc.Value);
            query = query.Where(h => h.PerformedAtUtc >= from);
        }

        if (filter.ToUtc.HasValue)
        {
            var to = EnsureUtc(filter.ToUtc.Value);
            query = query.Where(h => h.PerformedAtUtc <= to);
        }

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var skip = (filter.Page - 1) * size;

        var items = await query
            .OrderByDescending(h => h.PerformedAtUtc)
            .Skip(skip)
            .Take(size)
            .Select(h => new TransferHistoryDto(
                h.TransferId,
                h.PerformedAtUtc,
                h.Player.PlayerGuid,
                h.PlayerId,
                h.Player.Name,
                h.FromTeamId,
                h.FromTeam != null ? h.FromTeam.TeamName : null,
                h.ToTeamId,
                h.ToTeam != null ? h.ToTeam.TeamName : null,
                h.Amount,
                (int)h.Type,
                h.Type.ToDisplayName(),
                h.Notes,
                h.PerformedBy))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<TransferHistoryDto>(items, total, filter.Page, size);
    }

    private static DateTime EnsureUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
}
