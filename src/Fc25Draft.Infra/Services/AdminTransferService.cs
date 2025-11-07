using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Enums;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
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
    private readonly ITransactionLogService _transactionLogService;

    public AdminTransferService(
        DraftDbContext dbContext,
        ITransactionLogService transactionLogService,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _transactionLogService = transactionLogService ?? throw new ArgumentNullException(nameof(transactionLogService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task AdjustBudgetAsync(string adminToken, Guid teamId, decimal delta, string? reason, CancellationToken ct)
    {
        if (teamId == Guid.Empty) throw new ArgumentException("Time inválido.", nameof(teamId));
        if (delta == 0m) throw new ArgumentException("O ajuste deve ser diferente de zero.", nameof(delta));

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);
        var normalizedReason = NormalizeReason(reason);
        var normalizedDelta = decimal.Round(delta, 2, MidpointRounding.AwayFromZero);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await RunInExecutionStrategyAsync(async ctoken =>
        {
            var team = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.TeamId == teamId, ctoken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Time {teamId} não encontrado.");

            team.Budget = decimal.Round(team.Budget + normalizedDelta, 2, MidpointRounding.AwayFromZero);

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.AdjustBudget,
                PerformedBy = adminTokenGuid.ToString(),
                PayloadJson = JsonSerializer.Serialize(new { teamId, delta = normalizedDelta, reason = normalizedReason }, JsonOptions),
                CreatedAtUtc = now
            };

            await _dbContext.AdminActionsLogs.AddAsync(logEntry, ctoken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ctoken).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    private static string FormatPlayerList(IReadOnlyCollection<Player> players)
        => players.Count == 0 ? "Sem jogadores" : string.Join(", ", players.Select(p => p.Name));

    public async Task SellAsync(string adminToken, Guid fromTeamId, Guid toTeamId, Guid[] playerIds, decimal amount, string? reason, CancellationToken ct)
    {
        if (fromTeamId == Guid.Empty) throw new ArgumentException("Time de origem inválido.", nameof(fromTeamId));
        if (toTeamId == Guid.Empty) throw new ArgumentException("Time de destino inválido.", nameof(toTeamId));
        if (fromTeamId == toTeamId) throw new ArgumentException("Os times de origem e destino devem ser diferentes.");
        if (playerIds is null || playerIds.Length == 0) throw new ArgumentException("Informe ao menos um jogador.", nameof(playerIds));
        if (playerIds.Any(id => id == Guid.Empty)) throw new ArgumentException("Jogador inválido na lista.", nameof(playerIds));
        var distinctPlayerIds = playerIds.Distinct().ToArray();
        if (distinctPlayerIds.Length != playerIds.Length) throw new ArgumentException("Jogadores duplicados não são permitidos.", nameof(playerIds));
        if (amount < 0m) throw new ArgumentException("O valor não pode ser negativo.", nameof(amount));

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);
        var normalizedReason = NormalizeReason(reason);
        var normalizedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await RunInExecutionStrategyAsync(async ctoken =>
        {
            var teams = await _dbContext.Teams
                .Where(t => t.TeamId == fromTeamId || t.TeamId == toTeamId)
                .ToListAsync(ctoken).ConfigureAwait(false);

            var fromTeam = teams.FirstOrDefault(t => t.TeamId == fromTeamId)
                ?? throw new KeyNotFoundException($"Time vendedor {fromTeamId} não encontrado.");
            var toTeam = teams.FirstOrDefault(t => t.TeamId == toTeamId)
                ?? throw new KeyNotFoundException($"Time comprador {toTeamId} não encontrado.");

            var players = await _dbContext.Players
                .Include(p => p.TeamRosters)
                .Where(p => distinctPlayerIds.Contains(p.PlayerGuid))
                .ToListAsync(ctoken).ConfigureAwait(false);

            if (players.Count != distinctPlayerIds.Length)
                throw new InvalidOperationException("Um ou mais jogadores informados não foram encontrados.");
            if (players.Any(p => !PlayerBelongsToTeam(p, fromTeamId)))
                throw new InvalidOperationException("Todos os jogadores devem pertencer ao time de origem.");

            var playerNumericIds = players.Select(p => p.PlayerId).ToArray();
            var hasActiveListings = await _dbContext.MarketItems.AsNoTracking()
                .AnyAsync(i => playerNumericIds.Contains(i.PlayerId) && i.Status == MarketItemStatus.Active, ctoken)
                .ConfigureAwait(false);
            if (hasActiveListings)
                throw new InvalidOperationException("Remova o jogador do mercado antes de concluir a venda.");

            var currentBuyerCount = await _dbContext.Players
                .CountAsync(p => p.CurrentTeamId == toTeamId, ctoken).ConfigureAwait(false);
            if (currentBuyerCount + players.Count > 23)
                throw new InvalidOperationException("O time comprador excederia o limite de 23 jogadores.");

            var availableBudget = decimal.Round(toTeam.Budget - toTeam.BudgetBlocked, 2, MidpointRounding.AwayFromZero);
            if (availableBudget < normalizedAmount)
                throw new InvalidOperationException("Saldo insuficiente no time comprador.");

            toTeam.Budget = decimal.Round(toTeam.Budget - normalizedAmount, 2, MidpointRounding.AwayFromZero);
            fromTeam.Budget = decimal.Round(fromTeam.Budget + normalizedAmount, 2, MidpointRounding.AwayFromZero);

            foreach (var player in players) player.CurrentTeamId = toTeamId;

            var rosterEntries = await _dbContext.TeamRosters
                .Where(r => playerNumericIds.Contains(r.PlayerId))
                .ToListAsync(ctoken).ConfigureAwait(false);

            foreach (var entry in rosterEntries)
                if (entry.TeamId != toTeamId) _dbContext.TeamRosters.Remove(entry);

            var buyerRosterSet = rosterEntries.Where(e => e.TeamId == toTeamId).Select(e => e.PlayerId).ToHashSet();
            foreach (var player in players)
                if (!buyerRosterSet.Contains(player.PlayerId))
                    await _dbContext.TeamRosters.AddAsync(new TeamRoster { PlayerId = player.PlayerId, TeamId = toTeamId }, ctoken).ConfigureAwait(false);

            var culture = CultureInfo.GetCultureInfo("pt-BR");
            var formattedAmount = normalizedAmount.ToString("N2", culture);
            var notes = $"Lote de {players.Count} jogadores por R${formattedAmount}";

            var historyEntries = players.Select(player => new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = TransferType.TeamSale,
                PlayerId = player.PlayerId,
                FromTeamId = fromTeamId,
                ToTeamId = toTeamId,
                Amount = normalizedAmount,
                Payout = normalizedAmount,
                OldOverall = player.Overall,
                NewOverall = player.Overall,
                Notes = notes,
                PerformedBy = adminTokenGuid.ToString(),
                OccurredAtUtc = now
            }).ToList();

            await _dbContext.TransferHistories.AddRangeAsync(historyEntries, ctoken).ConfigureAwait(false);

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.SellPlayers,
                PerformedBy = adminTokenGuid.ToString(),
                PayloadJson = JsonSerializer.Serialize(new { fromTeamId, toTeamId, playerIds = distinctPlayerIds, amount = normalizedAmount, reason = normalizedReason }, JsonOptions),
                CreatedAtUtc = now
            };

            await _dbContext.AdminActionsLogs.AddAsync(logEntry, ctoken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ctoken).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task SwapAsync(string adminToken, Guid teamAId, Guid[]? playersFromA, Guid teamBId, Guid[]? playersFromB, decimal cashAdjustFromAToB, string? reason, CancellationToken ct)
    {
        if (teamAId == Guid.Empty) throw new ArgumentException("Time A inválido.", nameof(teamAId));
        if (teamBId == Guid.Empty) throw new ArgumentException("Time B inválido.", nameof(teamBId));
        if (teamAId == teamBId) throw new ArgumentException("Os times devem ser diferentes.");

        var playersFromAIds = playersFromA ?? Array.Empty<Guid>();
        var playersFromBIds = playersFromB ?? Array.Empty<Guid>();

        if (playersFromAIds.Length == 0 && playersFromBIds.Length == 0)
            throw new ArgumentException("Informe ao menos um jogador na troca.");
        if (playersFromAIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Jogador inválido na lista do time A.", nameof(playersFromA));
        if (playersFromBIds.Any(id => id == Guid.Empty))
            throw new ArgumentException("Jogador inválido na lista do time B.", nameof(playersFromB));
        if (playersFromAIds.Distinct().Count() != playersFromAIds.Length)
            throw new ArgumentException("Jogadores duplicados em Time A não são permitidos.", nameof(playersFromA));
        if (playersFromBIds.Distinct().Count() != playersFromBIds.Length)
            throw new ArgumentException("Jogadores duplicados em Time B não são permitidos.", nameof(playersFromB));
        if (playersFromAIds.Intersect(playersFromBIds).Any())
            throw new ArgumentException("Um jogador não pode estar em ambos os lados da troca.");

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);
        var normalizedReason = NormalizeReason(reason);
        var normalizedCashAdjust = decimal.Round(cashAdjustFromAToB, 2, MidpointRounding.AwayFromZero);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var culture = CultureInfo.GetCultureInfo("pt-BR");

        var allPlayerGuids = playersFromAIds.Concat(playersFromBIds).ToArray();
        var orderA = playersFromAIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);
        var orderB = playersFromBIds
            .Select((id, index) => new { id, index })
            .ToDictionary(x => x.id, x => x.index);

        await RunInExecutionStrategyAsync(async ctoken =>
        {
            var teams = await _dbContext.Teams
                .Where(t => t.TeamId == teamAId || t.TeamId == teamBId)
                .ToListAsync(ctoken).ConfigureAwait(false);

            var teamA = teams.FirstOrDefault(t => t.TeamId == teamAId)
                ?? throw new KeyNotFoundException($"Time {teamAId} não encontrado.");
            var teamB = teams.FirstOrDefault(t => t.TeamId == teamBId)
                ?? throw new KeyNotFoundException($"Time {teamBId} não encontrado.");

            var players = allPlayerGuids.Length > 0
                ? await _dbContext.Players
                    .Include(p => p.TeamRosters)
                    .Where(p => allPlayerGuids.Contains(p.PlayerGuid))
                    .ToListAsync(ctoken).ConfigureAwait(false)
                : new List<Player>();

            if (players.Count != allPlayerGuids.Length)
                throw new InvalidOperationException("Um ou mais jogadores informados não foram encontrados.");

            var aEntities = players
                .Where(p => orderA.ContainsKey(p.PlayerGuid))
                .OrderBy(p => orderA[p.PlayerGuid])
                .ToList();
            var bEntities = players
                .Where(p => orderB.ContainsKey(p.PlayerGuid))
                .OrderBy(p => orderB[p.PlayerGuid])
                .ToList();

            if (aEntities.Count != playersFromAIds.Length)
                throw new InvalidOperationException("Jogadores de Time A não encontrados.");
            if (bEntities.Count != playersFromBIds.Length)
                throw new InvalidOperationException("Jogadores de Time B não encontrados.");

            if (aEntities.Any(p => !PlayerBelongsToTeam(p, teamAId)))
                throw new InvalidOperationException("Todos os jogadores do Time A devem pertencer ao próprio time.");
            if (bEntities.Any(p => !PlayerBelongsToTeam(p, teamBId)))
                throw new InvalidOperationException("Todos os jogadores do Time B devem pertencer ao próprio time.");

            if (allPlayerGuids.Length > 0)
            {
                var playerNumericIds = players.Select(p => p.PlayerId).ToArray();

                var hasActiveListings = await _dbContext.MarketItems.AsNoTracking()
                    .AnyAsync(i => playerNumericIds.Contains(i.PlayerId) && i.Status == MarketItemStatus.Active, ctoken)
                    .ConfigureAwait(false);

                if (hasActiveListings)
                    throw new InvalidOperationException("Remova os jogadores do mercado antes de concluir a troca.");
            }

            var teamAPlayerCount = await _dbContext.Players
                .CountAsync(p => p.CurrentTeamId == teamAId, ctoken).ConfigureAwait(false);
            var teamBPlayerCount = await _dbContext.Players
                .CountAsync(p => p.CurrentTeamId == teamBId, ctoken).ConfigureAwait(false);

            var teamAFinalCount = teamAPlayerCount - aEntities.Count + bEntities.Count;
            var teamBFinalCount = teamBPlayerCount - bEntities.Count + aEntities.Count;

            if (teamAFinalCount > 23)
                throw new InvalidOperationException("Time A excederia o limite de 23 jogadores.");
            if (teamBFinalCount > 23)
                throw new InvalidOperationException("Time B excederia o limite de 23 jogadores.");

            if (normalizedCashAdjust > 0m)
            {
                var availableA = decimal.Round(teamA.Budget - teamA.BudgetBlocked, 2, MidpointRounding.AwayFromZero);
                if (availableA < normalizedCashAdjust)
                    throw new InvalidOperationException("Saldo insuficiente no Time A para o ajuste financeiro.");
            }
            else if (normalizedCashAdjust < 0m)
            {
                var needed = Math.Abs(normalizedCashAdjust);
                var availableB = decimal.Round(teamB.Budget - teamB.BudgetBlocked, 2, MidpointRounding.AwayFromZero);
                if (availableB < needed)
                    throw new InvalidOperationException("Saldo insuficiente no Time B para o ajuste financeiro.");
            }

            foreach (var p in aEntities) p.CurrentTeamId = teamBId;
            foreach (var p in bEntities) p.CurrentTeamId = teamAId;

            if (allPlayerGuids.Length > 0)
            {
                var playerNumericIds = players.Select(p => p.PlayerId).ToArray();
                var rosterEntries = await _dbContext.TeamRosters
                    .Where(r => playerNumericIds.Contains(r.PlayerId))
                    .ToListAsync(ctoken).ConfigureAwait(false);

                var aNumIds = aEntities.Select(p => p.PlayerId).ToHashSet();
                var bNumIds = bEntities.Select(p => p.PlayerId).ToHashSet();

                foreach (var entry in rosterEntries)
                {
                    if (aNumIds.Contains(entry.PlayerId) && entry.TeamId != teamBId)
                        _dbContext.TeamRosters.Remove(entry);
                    else if (bNumIds.Contains(entry.PlayerId) && entry.TeamId != teamAId)
                        _dbContext.TeamRosters.Remove(entry);
                }

                var rosterBPlayers = rosterEntries.Where(e => e.TeamId == teamBId).Select(e => e.PlayerId).ToHashSet();
                foreach (var pid in aNumIds)
                    if (!rosterBPlayers.Contains(pid))
                        await _dbContext.TeamRosters.AddAsync(new TeamRoster { PlayerId = pid, TeamId = teamBId }, ctoken).ConfigureAwait(false);

                var rosterAPlayers = rosterEntries.Where(e => e.TeamId == teamAId).Select(e => e.PlayerId).ToHashSet();
                foreach (var pid in bNumIds)
                    if (!rosterAPlayers.Contains(pid))
                        await _dbContext.TeamRosters.AddAsync(new TeamRoster { PlayerId = pid, TeamId = teamAId }, ctoken).ConfigureAwait(false);
            }

            if (normalizedCashAdjust > 0m)
            {
                teamA.Budget = decimal.Round(teamA.Budget - normalizedCashAdjust, 2, MidpointRounding.AwayFromZero);
                teamB.Budget = decimal.Round(teamB.Budget + normalizedCashAdjust, 2, MidpointRounding.AwayFromZero);
            }
            else if (normalizedCashAdjust < 0m)
            {
                var abs = Math.Abs(normalizedCashAdjust);
                teamB.Budget = decimal.Round(teamB.Budget - abs, 2, MidpointRounding.AwayFromZero);
                teamA.Budget = decimal.Round(teamA.Budget + abs, 2, MidpointRounding.AwayFromZero);
            }

            string adjustmentDescription;
            if (normalizedCashAdjust > 0m)
                adjustmentDescription = $"{teamB.TeamName} recebe {normalizedCashAdjust.ToString("N2", culture)}";
            else if (normalizedCashAdjust < 0m)
                adjustmentDescription = $"{teamA.TeamName} recebe{Math.Abs(normalizedCashAdjust).ToString("N2", culture)}";
            else
                adjustmentDescription = "";

            var notes = $"Troca! {teamA.TeamName} recebe ({FormatPlayerList(bEntities)}), {teamB.TeamName} recebe ({FormatPlayerList(aEntities)}). {adjustmentDescription}";

            var cashAdjustAmount = Math.Abs(normalizedCashAdjust);
            var hasCashAdjust = cashAdjustAmount > 0m;
            var amountCarrierPlayerId = hasCashAdjust
                ? players
                    .OrderByDescending(p => p.Overall)
                    .ThenBy(p => p.PlayerId)
                    .First().PlayerId
                : (int?)null;

            var histories = new List<TransferHistory>();
            foreach (var p in aEntities)
            {
                var amount = !hasCashAdjust
                    ? (decimal?)null
                    : amountCarrierPlayerId == p.PlayerId
                        ? cashAdjustAmount
                        : 0m;

                histories.Add(new TransferHistory
                {
                    TransferId = Guid.NewGuid(),
                    Type = TransferType.TeamTrade,
                    PlayerId = p.PlayerId,
                    FromTeamId = teamAId,
                    ToTeamId = teamBId,
                    Amount = amount,
                    Payout = amount,
                    OldOverall = p.Overall,
                    NewOverall = p.Overall,
                    Notes = notes,
                    PerformedBy = adminTokenGuid.ToString(),
                    OccurredAtUtc = now
                });
            }
            foreach (var p in bEntities)
            {
                var amount = !hasCashAdjust
                    ? (decimal?)null
                    : amountCarrierPlayerId == p.PlayerId
                        ? cashAdjustAmount
                        : 0m;

                histories.Add(new TransferHistory
                {
                    TransferId = Guid.NewGuid(),
                    Type = TransferType.TeamTrade,
                    PlayerId = p.PlayerId,
                    FromTeamId = teamBId,
                    ToTeamId = teamAId,
                    Amount = amount,
                    Payout = amount,
                    OldOverall = p.Overall,
                    NewOverall = p.Overall,
                    Notes = notes,
                    PerformedBy = adminTokenGuid.ToString(),
                    OccurredAtUtc = now
                });
            }
            if (histories.Count > 0)
                await _dbContext.TransferHistories.AddRangeAsync(histories, ctoken).ConfigureAwait(false);

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
            await _dbContext.AdminActionsLogs.AddAsync(logEntry, ctoken).ConfigureAwait(false);

            await _dbContext.SaveChangesAsync(ctoken).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
    }

    public async Task MoveAsync(string adminToken, Guid playerId, Guid toTeamId, string? reason, CancellationToken ct)
    {
        if (playerId == Guid.Empty) throw new ArgumentException("Jogador inválido.", nameof(playerId));
        if (toTeamId == Guid.Empty) throw new ArgumentException("Time de destino inválido.", nameof(toTeamId));

        var adminTokenGuid = await EnsureValidAdminTokenAsync(adminToken, ct).ConfigureAwait(false);
        var normalizedReason = NormalizeReason(reason);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        await RunInExecutionStrategyAsync(async ctoken =>
        {
            var player = await _dbContext.Players
                .FirstOrDefaultAsync(p => p.PlayerGuid == playerId, ctoken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Jogador não encontrado.");

            var fromTeamId = player.CurrentTeamId;

            var toTeamExists = await _dbContext.Teams.AnyAsync(t => t.TeamId == toTeamId, ctoken).ConfigureAwait(false);
            if (!toTeamExists) throw new KeyNotFoundException("Time de destino não encontrado.");

            var hasActiveListing = await _dbContext.MarketItems.AsNoTracking()
                .AnyAsync(i => i.PlayerId == player.PlayerId && i.Status == MarketItemStatus.Active, ctoken)
                .ConfigureAwait(false);
            if (hasActiveListing) throw new InvalidOperationException("Remova o jogador do mercado antes de concluir a movimentação.");

            var currentToTeamCount = await _dbContext.Players.CountAsync(p => p.CurrentTeamId == toTeamId, ctoken).ConfigureAwait(false);
            var finalToTeamCount = currentToTeamCount + (fromTeamId != toTeamId ? 1 : 0);
            if (finalToTeamCount > 23) throw new InvalidOperationException("O time de destino excederia o limite de 23 jogadores.");

            player.CurrentTeamId = toTeamId;

            var rosterEntries = await _dbContext.TeamRosters
                .Where(r => r.PlayerId == player.PlayerId)
                .ToListAsync(ctoken).ConfigureAwait(false);

            foreach (var entry in rosterEntries)
                if (entry.TeamId != toTeamId) _dbContext.TeamRosters.Remove(entry);

            if (!rosterEntries.Any(e => e.TeamId == toTeamId))
            {
                await _dbContext.TeamRosters.AddAsync(new TeamRoster { PlayerId = player.PlayerId, TeamId = toTeamId }, ctoken).ConfigureAwait(false);
            }

            var historyEntry = new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = TransferType.TeamTrade,
                PlayerId = player.PlayerId,
                FromTeamId = fromTeamId,
                ToTeamId = toTeamId,
                Amount = null,
                Payout = null,
                OldOverall = player.Overall,
                NewOverall = player.Overall,
                Notes = normalizedReason,
                PerformedBy = adminTokenGuid.ToString(),
                OccurredAtUtc = now
            };

            await _dbContext.TransferHistories.AddAsync(historyEntry, ctoken).ConfigureAwait(false);

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.MovePlayer,
                PerformedBy = adminTokenGuid.ToString(),
                PayloadJson = JsonSerializer.Serialize(new { playerId, fromTeamId, toTeamId, reason = normalizedReason }, JsonOptions),
                CreatedAtUtc = now
            };

            await _dbContext.AdminActionsLogs.AddAsync(logEntry, ctoken).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ctoken).ConfigureAwait(false);
        }, ct).ConfigureAwait(false);
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

    private Task RunInExecutionStrategyAsync(Func<CancellationToken, Task> work, CancellationToken ct)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            await work(ct);
            await tx.CommitAsync(ct);
        });
    }
}
