using System.Globalization;
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

    public async Task SellAsync(
        string adminToken,
        Guid fromTeamId,
        Guid toTeamId,
        Guid[] playerIds,
        decimal amount,
        string reason,
        CancellationToken ct)
    {
        if (fromTeamId == Guid.Empty)
        {
            throw new ArgumentException("Time de origem inválido.", nameof(fromTeamId));
        }

        if (toTeamId == Guid.Empty)
        {
            throw new ArgumentException("Time de destino inválido.", nameof(toTeamId));
        }

        if (fromTeamId == toTeamId)
        {
            throw new ArgumentException("Os times de origem e destino devem ser diferentes.");
        }

        if (playerIds is null || playerIds.Length == 0)
        {
            throw new ArgumentException("Informe ao menos um jogador.", nameof(playerIds));
        }

        if (playerIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Jogador inválido na lista.", nameof(playerIds));
        }

        var distinctPlayerIds = playerIds.Distinct().ToArray();
        if (distinctPlayerIds.Length != playerIds.Length)
        {
            throw new ArgumentException("Jogadores duplicados não são permitidos.", nameof(playerIds));
        }

        if (amount < 0m)
        {
            throw new ArgumentException("O valor não pode ser negativo.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Informe um motivo para a venda.", nameof(reason));
        }

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);
        var normalizedReason = reason.Trim();
        var normalizedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var teams = await _dbContext.Teams
                .Where(t => t.TeamId == fromTeamId || t.TeamId == toTeamId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var fromTeam = teams.FirstOrDefault(t => t.TeamId == fromTeamId)
                ?? throw new KeyNotFoundException($"Time vendedor {fromTeamId} não encontrado.");

            var toTeam = teams.FirstOrDefault(t => t.TeamId == toTeamId)
                ?? throw new KeyNotFoundException($"Time comprador {toTeamId} não encontrado.");

            var players = await _dbContext.Players
                .Where(p => distinctPlayerIds.Contains(p.PlayerGuid))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (players.Count != distinctPlayerIds.Length)
            {
                throw new InvalidOperationException("Um ou mais jogadores informados não foram encontrados.");
            }

            if (players.Any(p => p.CurrentTeamId != fromTeamId))
            {
                throw new InvalidOperationException("Todos os jogadores devem pertencer ao time de origem.");
            }

            var playerNumericIds = players.Select(p => p.PlayerId).ToArray();

            var hasActiveListings = await _dbContext.MarketItems
                .AsNoTracking()
                .AnyAsync(
                    i => playerNumericIds.Contains(i.PlayerId)
                        && (i.Status == MarketItemStatus.Active || i.Status == MarketItemStatus.LeaderChanged),
                    ct)
                .ConfigureAwait(false);

            if (hasActiveListings)
            {
                throw new InvalidOperationException("Remova o jogador do mercado antes de concluir a venda.");
            }

            var currentBuyerCount = await _dbContext.Players
                .CountAsync(p => p.CurrentTeamId == toTeamId, ct)
                .ConfigureAwait(false);

            if (currentBuyerCount + players.Count > 23)
            {
                throw new InvalidOperationException("O time comprador excederia o limite de 23 jogadores.");
            }

            var availableBudget = decimal.Round(toTeam.Budget - toTeam.BudgetBlocked, 2, MidpointRounding.AwayFromZero);
            if (availableBudget < normalizedAmount)
            {
                throw new InvalidOperationException("Saldo insuficiente no time comprador.");
            }

            toTeam.Budget = decimal.Round(toTeam.Budget - normalizedAmount, 2, MidpointRounding.AwayFromZero);

            foreach (var player in players)
            {
                player.CurrentTeamId = toTeamId;
            }

            var rosterEntries = await _dbContext.TeamRosters
                .Where(r => playerNumericIds.Contains(r.PlayerId))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var entry in rosterEntries)
            {
                if (entry.TeamId != toTeamId)
                {
                    _dbContext.TeamRosters.Remove(entry);
                }
            }

            var buyerRosterSet = rosterEntries
                .Where(e => e.TeamId == toTeamId)
                .Select(e => e.PlayerId)
                .ToHashSet();

            foreach (var player in players)
            {
                if (!buyerRosterSet.Contains(player.PlayerId))
                {
                    await _dbContext.TeamRosters.AddAsync(new TeamRoster
                    {
                        PlayerId = player.PlayerId,
                        TeamId = toTeamId
                    }, ct).ConfigureAwait(false);
                }
            }

            var culture = CultureInfo.GetCultureInfo("pt-BR");
            var formattedAmount = normalizedAmount.ToString("N2", culture);
            var notes = $"Lote de {players.Count} jogadores por R${formattedAmount}";

            var historyEntries = players.Select(player => new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = TransferType.Sale,
                PlayerId = player.PlayerId,
                FromTeamId = fromTeamId,
                ToTeamId = toTeamId,
                Amount = normalizedAmount,
                Notes = notes,
                PerformedBy = adminTokenGuid.ToString(),
                PerformedAtUtc = now
            }).ToList();

            await _dbContext.TransferHistories.AddRangeAsync(historyEntries, ct).ConfigureAwait(false);

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.SellPlayers,
                PerformedBy = adminTokenGuid.ToString(),
                PayloadJson = JsonSerializer.Serialize(new
                {
                    fromTeamId,
                    toTeamId,
                    playerIds = distinctPlayerIds,
                    amount = normalizedAmount,
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
