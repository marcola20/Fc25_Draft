using System.Linq;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class TransfersQueryService : ITransfersQueryService
{
    private readonly DraftDbContext _dbContext;

    public TransfersQueryService(DraftDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PagedResult<TransferHistoryDto>> QueryHistoryAsync(TransfersFilter filter, CancellationToken ct)
    {
        if (filter is null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        if (filter.Page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter.Page), "Página deve ser maior ou igual a 1.");
        }

        if (filter.PageSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(filter.PageSize), "Tamanho de página deve ser maior ou igual a 1.");
        }

        var normalizedPageSize = Math.Min(filter.PageSize, 200);

        var query = _dbContext.TransferHistories
            .AsNoTracking()
            .Include(t => t.Player)
            .Include(t => t.FromTeam)
            .Include(t => t.ToTeam)
            .AsQueryable();

        if (filter.TeamId.HasValue)
        {
            var teamId = filter.TeamId.Value;
            query = query.Where(t => t.FromTeamId == teamId || t.ToTeamId == teamId);
        }

        if (filter.PlayerId.HasValue && filter.PlayerId.Value != Guid.Empty)
        {
            var playerPublicId = filter.PlayerId.Value;
            query = query.Where(t => t.PlayerPublicId == playerPublicId);
        }

        if (filter.Type.HasValue)
        {
            query = query.Where(t => (int)t.Type == filter.Type.Value);
        }

        if (filter.FromUtc.HasValue)
        {
            query = query.Where(t => t.PerformedAtUtc >= filter.FromUtc.Value);
        }

        if (filter.ToUtc.HasValue)
        {
            query = query.Where(t => t.PerformedAtUtc <= filter.ToUtc.Value);
        }

        query = query.OrderByDescending(t => t.PerformedAtUtc);

        var total = await query.CountAsync(ct).ConfigureAwait(false);

        var items = await query
            .Skip((filter.Page - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(t => new TransferHistoryDto(
                t.TransferId,
                (int)t.Type,
                t.PlayerPublicId,
                t.Player.Name,
                t.FromTeamId,
                t.FromTeam != null ? t.FromTeam.TeamName : null,
                t.ToTeamId,
                t.ToTeam != null ? t.ToTeam.TeamName : null,
                t.Amount,
                t.Notes,
                t.PerformedAtUtc))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new PagedResult<TransferHistoryDto>(items, total);
    }
}
