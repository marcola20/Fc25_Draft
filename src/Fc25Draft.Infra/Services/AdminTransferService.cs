using System.Text.Json;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public partial class AdminTransferService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly DraftDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public AdminTransferService(
        DraftDbContext dbContext,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task AdjustBudgetAsync(string adminToken, Guid teamId, decimal delta, string reason, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (delta == 0m)
        {
            throw new ArgumentException("O ajuste deve ser diferente de zero.", nameof(delta));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Informe um motivo para o ajuste.", nameof(reason));
        }

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);

        var normalizedReason = reason.Trim();
        var normalizedDelta = decimal.Round(delta, 2, MidpointRounding.AwayFromZero);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var team = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Time {teamId} não encontrado.");

            team.Budget = decimal.Round(team.Budget + normalizedDelta, 2, MidpointRounding.AwayFromZero);

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.AdjustBudget,
                PerformedBy = adminTokenGuid.ToString(),
                PayloadJson = JsonSerializer.Serialize(new
                {
                    teamId,
                    delta = normalizedDelta,
                    reason = normalizedReason
                }, JsonOptions),
                CreatedAtUtc = now
            };

            await _dbContext.AdminActionsLogs.AddAsync(logEntry, ct).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    public async Task CancelMarketItemAsync(string adminToken, Guid itemId, string reason, CancellationToken ct)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("Item inválido.", nameof(itemId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Informe um motivo para o cancelamento.", nameof(reason));
        }

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);
        var normalizedReason = reason.Trim();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var item = await _dbContext.MarketItems
                .Include(i => i.Bids)
                .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Item de mercado não encontrado.");

            if (item.Status != MarketItemStatus.Active)
            {
                throw new AdminConflictException("Somente itens ativos sem lances podem ser cancelados.");
            }

            if (item.CurrentLeaderTeamId.HasValue)
            {
                throw new AdminConflictException("O item possui um líder atual e não pode ser cancelado.");
            }

            item.Status = MarketItemStatus.Cancelled;
            item.CurrentLeaderAmount = null;
            item.CurrentLeaderTeamId = null;
            item.LastUpdateUtc = now;
            item.WinnerTeamId = null;

            if (item.ExpiresAtUtc > now)
            {
                item.ExpiresAtUtc = now;
            }

            var lastBid = item.Bids
                .OrderByDescending(b => b.CreatedAtUtc)
                .FirstOrDefault();

            if (lastBid is not null)
            {
                var team = await _dbContext.Teams
                    .FirstOrDefaultAsync(t => t.TeamId == lastBid.TeamId, ct)
                    .ConfigureAwait(false);

                if (team is not null && team.BudgetBlocked > 0m)
                {
                    var releaseAmount = Math.Min(team.BudgetBlocked, decimal.Round(lastBid.Amount, 2, MidpointRounding.AwayFromZero));
                    team.BudgetBlocked = decimal.Round(team.BudgetBlocked - releaseAmount, 2, MidpointRounding.AwayFromZero);
                }
            }

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.CancelMarketItem,
                PerformedBy = adminTokenGuid.ToString(),
                PayloadJson = JsonSerializer.Serialize(new
                {
                    itemId,
                    reason = normalizedReason
                }, JsonOptions),
                CreatedAtUtc = now
            };

            await _dbContext.AdminActionsLogs.AddAsync(logEntry, ct).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(ct).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<Guid> EnsureValidAdminTokenAsync(string adminToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(adminToken))
        {
            throw new AdminForbiddenException("Token de administrador ausente.");
        }

        if (!Guid.TryParse(adminToken.Trim(), out var tokenGuid))
        {
            throw new AdminForbiddenException("Token de administrador inválido.");
        }

        var tokenExists = await _dbContext.AdminTokens
            .AsNoTracking()
            .AnyAsync(t => t.Token == tokenGuid, ct)
            .ConfigureAwait(false);

        if (!tokenExists)
        {
            throw new AdminForbiddenException("Token de administrador inválido.");
        }

        return tokenGuid;
    }
}
