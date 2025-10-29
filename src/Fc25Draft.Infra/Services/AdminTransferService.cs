using System.Collections.Generic;
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

    public async Task AdjustBudgetAsync(string adminToken, Guid teamId, decimal delta, string? reason, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("Time inválido.", nameof(teamId));
        }

        if (delta == 0m)
        {
            throw new ArgumentException("O ajuste deve ser diferente de zero.", nameof(delta));
        }

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);

        var normalizedReason = NormalizeReason(reason);
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

    public async Task CancelMarketItemAsync(string adminToken, Guid itemId, string? reason, CancellationToken ct)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("Item inválido.", nameof(itemId));
        }

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);
        var normalizedReason = NormalizeReason(reason);
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
        string? reason,
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

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);
        var normalizedReason = NormalizeReason(reason);
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
                .Include(p => p.TeamRosters)
                .Where(p => distinctPlayerIds.Contains(p.PlayerGuid))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (players.Count != distinctPlayerIds.Length)
            {
                throw new InvalidOperationException("Um ou mais jogadores informados não foram encontrados.");
            }

            if (players.Any(p => !PlayerBelongsToTeam(p, fromTeamId)))
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
            fromTeam.Budget = decimal.Round(fromTeam.Budget + normalizedAmount, 2, MidpointRounding.AwayFromZero);

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

    public async Task SwapAsync(
        string adminToken,
        Guid teamAId,
        Guid[]? playersFromA,
        Guid teamBId,
        Guid[]? playersFromB,
        decimal cashAdjustFromAToB,
        string? reason,
        CancellationToken ct)
    {
        if (teamAId == Guid.Empty)
        {
            throw new ArgumentException("Time A inválido.", nameof(teamAId));
        }

        if (teamBId == Guid.Empty)
        {
            throw new ArgumentException("Time B inválido.", nameof(teamBId));
        }

        if (teamAId == teamBId)
        {
            throw new ArgumentException("Os times devem ser diferentes.");
        }

        var playersFromAIds = playersFromA ?? Array.Empty<Guid>();
        var playersFromBIds = playersFromB ?? Array.Empty<Guid>();

        if (playersFromAIds.Length == 0 && playersFromBIds.Length == 0)
        {
            throw new ArgumentException("Informe ao menos um jogador na troca.");
        }

        if (playersFromAIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Jogador inválido na lista do time A.", nameof(playersFromA));
        }

        if (playersFromBIds.Any(id => id == Guid.Empty))
        {
            throw new ArgumentException("Jogador inválido na lista do time B.", nameof(playersFromB));
        }

        if (playersFromAIds.Distinct().Count() != playersFromAIds.Length)
        {
            throw new ArgumentException("Jogadores duplicados em Time A não são permitidos.", nameof(playersFromA));
        }

        if (playersFromBIds.Distinct().Count() != playersFromBIds.Length)
        {
            throw new ArgumentException("Jogadores duplicados em Time B não são permitidos.", nameof(playersFromB));
        }

        if (playersFromAIds.Intersect(playersFromBIds).Any())
        {
            throw new ArgumentException("Um jogador não pode estar em ambos os lados da troca.");
        }

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);
        var normalizedReason = NormalizeReason(reason);
        var normalizedCashAdjust = decimal.Round(cashAdjustFromAToB, 2, MidpointRounding.AwayFromZero);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var playerGuids = playersFromAIds.Concat(playersFromBIds).ToArray();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var teams = await _dbContext.Teams
                .Where(t => t.TeamId == teamAId || t.TeamId == teamBId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var teamA = teams.FirstOrDefault(t => t.TeamId == teamAId)
                ?? throw new KeyNotFoundException($"Time {teamAId} não encontrado.");

            var teamB = teams.FirstOrDefault(t => t.TeamId == teamBId)
                ?? throw new KeyNotFoundException($"Time {teamBId} não encontrado.");

            var players = playerGuids.Length > 0
                ? await _dbContext.Players
                    .Include(p => p.TeamRosters)
                    .Where(p => playerGuids.Contains(p.PlayerGuid))
                    .ToListAsync(ct)
                    .ConfigureAwait(false)
                : new List<Player>();

            if (players.Count != playerGuids.Length)
            {
                throw new InvalidOperationException("Um ou mais jogadores informados não foram encontrados.");
            }

            var playersFromAEntities = players
                .Where(p => playersFromAIds.Contains(p.PlayerGuid))
                .ToList();
            if (playersFromAEntities.Count != playersFromAIds.Length)
            {
                throw new InvalidOperationException("Jogadores de Time A não encontrados.");
            }

            if (playersFromAEntities.Any(p => !PlayerBelongsToTeam(p, teamAId)))
            {
                throw new InvalidOperationException("Todos os jogadores do Time A devem pertencer ao próprio time.");
            }

            var playersFromBEntities = players
                .Where(p => playersFromBIds.Contains(p.PlayerGuid))
                .ToList();
            if (playersFromBEntities.Count != playersFromBIds.Length)
            {
                throw new InvalidOperationException("Jogadores de Time B não encontrados.");
            }

            if (playersFromBEntities.Any(p => !PlayerBelongsToTeam(p, teamBId)))
            {
                throw new InvalidOperationException("Todos os jogadores do Time B devem pertencer ao próprio time.");
            }

            if (playerGuids.Length > 0)
            {
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
                    throw new InvalidOperationException("Remova os jogadores do mercado antes de concluir a troca.");
                }
            }

            var teamAPlayerCount = await _dbContext.Players
                .CountAsync(p => p.CurrentTeamId == teamAId, ct)
                .ConfigureAwait(false);
            var teamBPlayerCount = await _dbContext.Players
                .CountAsync(p => p.CurrentTeamId == teamBId, ct)
                .ConfigureAwait(false);

            var teamAFinalCount = teamAPlayerCount - playersFromAIds.Length + playersFromBIds.Length;
            if (teamAFinalCount > 23)
            {
                throw new InvalidOperationException("Time A excederia o limite de 23 jogadores.");
            }

            var teamBFinalCount = teamBPlayerCount - playersFromBIds.Length + playersFromAIds.Length;
            if (teamBFinalCount > 23)
            {
                throw new InvalidOperationException("Time B excederia o limite de 23 jogadores.");
            }

            if (normalizedCashAdjust > 0m)
            {
                var availableA = decimal.Round(teamA.Budget - teamA.BudgetBlocked, 2, MidpointRounding.AwayFromZero);
                if (availableA < normalizedCashAdjust)
                {
                    throw new InvalidOperationException("Saldo insuficiente no Time A para o ajuste financeiro.");
                }
            }
            else if (normalizedCashAdjust < 0m)
            {
                var adjustmentAbs = Math.Abs(normalizedCashAdjust);
                var availableB = decimal.Round(teamB.Budget - teamB.BudgetBlocked, 2, MidpointRounding.AwayFromZero);
                if (availableB < adjustmentAbs)
                {
                    throw new InvalidOperationException("Saldo insuficiente no Time B para o ajuste financeiro.");
                }
            }

            foreach (var player in playersFromAEntities)
            {
                player.CurrentTeamId = teamBId;
            }

            foreach (var player in playersFromBEntities)
            {
                player.CurrentTeamId = teamAId;
            }

            if (playerGuids.Length > 0)
            {
                var playerNumericIds = players.Select(p => p.PlayerId).ToArray();
                var rosterEntries = await _dbContext.TeamRosters
                    .Where(r => playerNumericIds.Contains(r.PlayerId))
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                var playersFromANumericIds = playersFromAEntities.Select(p => p.PlayerId).ToHashSet();
                var playersFromBNumericIds = playersFromBEntities.Select(p => p.PlayerId).ToHashSet();

                foreach (var entry in rosterEntries)
                {
                    if (playersFromANumericIds.Contains(entry.PlayerId) && entry.TeamId != teamBId)
                    {
                        _dbContext.TeamRosters.Remove(entry);
                    }
                    else if (playersFromBNumericIds.Contains(entry.PlayerId) && entry.TeamId != teamAId)
                    {
                        _dbContext.TeamRosters.Remove(entry);
                    }
                }

                var rosterBPlayers = rosterEntries
                    .Where(e => e.TeamId == teamBId)
                    .Select(e => e.PlayerId)
                    .ToHashSet();
                foreach (var playerId in playersFromANumericIds)
                {
                    if (!rosterBPlayers.Contains(playerId))
                    {
                        await _dbContext.TeamRosters.AddAsync(new TeamRoster
                        {
                            PlayerId = playerId,
                            TeamId = teamBId
                        }, ct).ConfigureAwait(false);
                    }
                }

                var rosterAPlayers = rosterEntries
                    .Where(e => e.TeamId == teamAId)
                    .Select(e => e.PlayerId)
                    .ToHashSet();
                foreach (var playerId in playersFromBNumericIds)
                {
                    if (!rosterAPlayers.Contains(playerId))
                    {
                        await _dbContext.TeamRosters.AddAsync(new TeamRoster
                        {
                            PlayerId = playerId,
                            TeamId = teamAId
                        }, ct).ConfigureAwait(false);
                    }
                }
            }

            if (normalizedCashAdjust > 0m)
            {
                teamA.Budget = decimal.Round(teamA.Budget - normalizedCashAdjust, 2, MidpointRounding.AwayFromZero);
                teamB.Budget = decimal.Round(teamB.Budget + normalizedCashAdjust, 2, MidpointRounding.AwayFromZero);
            }
            else if (normalizedCashAdjust < 0m)
            {
                var adjustmentAbs = Math.Abs(normalizedCashAdjust);
                teamB.Budget = decimal.Round(teamB.Budget - adjustmentAbs, 2, MidpointRounding.AwayFromZero);
                teamA.Budget = decimal.Round(teamA.Budget + adjustmentAbs, 2, MidpointRounding.AwayFromZero);
            }

            var culture = CultureInfo.GetCultureInfo("pt-BR");
            string adjustmentDescription;
            if (normalizedCashAdjust > 0m)
            {
                var formatted = normalizedCashAdjust.ToString("N2", culture);
                adjustmentDescription = $"Ajuste líquido: +{formatted} para {teamB.TeamName}.";
            }
            else if (normalizedCashAdjust < 0m)
            {
                var formatted = Math.Abs(normalizedCashAdjust).ToString("N2", culture);
                adjustmentDescription = $"Ajuste líquido: +{formatted} para {teamA.TeamName}.";
            }
            else
            {
                adjustmentDescription = "Ajuste líquido: 0,00.";
            }

            var notes = $"Troca {teamA.TeamName} ({playersFromAIds.Length}) ↔ {teamB.TeamName} ({playersFromBIds.Length}). {adjustmentDescription}";

            var historyEntries = new List<TransferHistory>();
            foreach (var player in playersFromAEntities)
            {
                historyEntries.Add(new TransferHistory
                {
                    TransferId = Guid.NewGuid(),
                    Type = TransferType.Swap,
                    PlayerId = player.PlayerId,
                    FromTeamId = teamAId,
                    ToTeamId = teamBId,
                    Amount = null,
                    Notes = notes,
                    PerformedBy = adminTokenGuid.ToString(),
                    PerformedAtUtc = now
                });
            }

            foreach (var player in playersFromBEntities)
            {
                historyEntries.Add(new TransferHistory
                {
                    TransferId = Guid.NewGuid(),
                    Type = TransferType.Swap,
                    PlayerId = player.PlayerId,
                    FromTeamId = teamBId,
                    ToTeamId = teamAId,
                    Amount = null,
                    Notes = notes,
                    PerformedBy = adminTokenGuid.ToString(),
                    PerformedAtUtc = now
                });
            }

            if (historyEntries.Count > 0)
            {
                await _dbContext.TransferHistories.AddRangeAsync(historyEntries, ct).ConfigureAwait(false);
            }

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.SwapPlayers,
                PerformedBy = adminTokenGuid.ToString(),
                PayloadJson = JsonSerializer.Serialize(new
                {
                    teamAId,
                    playersFromA = playersFromAIds,
                    teamBId,
                    playersFromB = playersFromBIds,
                    cashAdjustFromAToB = normalizedCashAdjust,
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

    public async Task MoveAsync(
        string adminToken,
        Guid playerId,
        Guid toTeamId,
        string? reason,
        CancellationToken ct)
    {
        if (playerId == Guid.Empty)
        {
            throw new ArgumentException("Jogador inválido.", nameof(playerId));
        }

        if (toTeamId == Guid.Empty)
        {
            throw new ArgumentException("Time de destino inválido.", nameof(toTeamId));
        }

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);
        var normalizedReason = NormalizeReason(reason);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        try
        {
            var player = await _dbContext.Players
                .FirstOrDefaultAsync(p => p.PlayerGuid == playerId, ct)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Jogador não encontrado.");

            var fromTeamId = player.CurrentTeamId;

            var toTeamExists = await _dbContext.Teams
                .AnyAsync(t => t.TeamId == toTeamId, ct)
                .ConfigureAwait(false);

            if (!toTeamExists)
            {
                throw new KeyNotFoundException("Time de destino não encontrado.");
            }

            var hasActiveListing = await _dbContext.MarketItems
                .AsNoTracking()
                .AnyAsync(
                    i => i.PlayerId == player.PlayerId
                        && (i.Status == MarketItemStatus.Active || i.Status == MarketItemStatus.LeaderChanged),
                    ct)
                .ConfigureAwait(false);

            if (hasActiveListing)
            {
                throw new InvalidOperationException("Remova o jogador do mercado antes de concluir a movimentação.");
            }

            var currentToTeamCount = await _dbContext.Players
                .CountAsync(p => p.CurrentTeamId == toTeamId, ct)
                .ConfigureAwait(false);

            var finalToTeamCount = currentToTeamCount;
            if (fromTeamId != toTeamId)
            {
                finalToTeamCount += 1;
            }

            if (finalToTeamCount > 23)
            {
                throw new InvalidOperationException("O time de destino excederia o limite de 23 jogadores.");
            }

            player.CurrentTeamId = toTeamId;

            var rosterEntries = await _dbContext.TeamRosters
                .Where(r => r.PlayerId == player.PlayerId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var entry in rosterEntries)
            {
                if (entry.TeamId != toTeamId)
                {
                    _dbContext.TeamRosters.Remove(entry);
                }
            }

            var hasRosterInToTeam = rosterEntries.Any(e => e.TeamId == toTeamId);
            if (!hasRosterInToTeam)
            {
                await _dbContext.TeamRosters.AddAsync(new TeamRoster
                {
                    PlayerId = player.PlayerId,
                    TeamId = toTeamId
                }, ct).ConfigureAwait(false);
            }

            var historyEntry = new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = TransferType.AdminMove,
                PlayerId = player.PlayerId,
                FromTeamId = fromTeamId,
                ToTeamId = toTeamId,
                Amount = null,
                Notes = normalizedReason,
                PerformedBy = adminTokenGuid.ToString(),
                PerformedAtUtc = now
            };

            await _dbContext.TransferHistories.AddAsync(historyEntry, ct).ConfigureAwait(false);

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.MovePlayer,
                PerformedBy = adminTokenGuid.ToString(),
                PayloadJson = JsonSerializer.Serialize(new
                {
                    playerId,
                    fromTeamId,
                    toTeamId,
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

    private static bool PlayerBelongsToTeam(Player player, Guid teamId)
    {
        if (player.CurrentTeamId == teamId)
        {
            return true;
        }

        if (player.TeamRosters is null)
        {
            return false;
        }

        return player.TeamRosters.Any(r => r.TeamId == teamId);
    }

    private static string? NormalizeReason(string? reason)
        => string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
}
