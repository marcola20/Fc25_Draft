using System.Data;
using System.Linq;
using System.Text.Json;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class AdminTransferService : IAdminTransferService
{
    private const int SquadLimit = 23;

    private readonly DraftDbContext _dbContext;
    private readonly IBudgetService _budgetService;
    private readonly TimeProvider _timeProvider;

    public AdminTransferService(
        DraftDbContext dbContext,
        IBudgetService budgetService,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _budgetService = budgetService ?? throw new ArgumentNullException(nameof(budgetService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TransferResult> SellAsync(
        string adminToken,
        Guid fromTeamId,
        Guid toTeamId,
        Guid[] playerIds,
        decimal amount,
        string reason,
        CancellationToken ct)
    {
        if (playerIds is null || playerIds.Length == 0)
        {
            throw new AdminValidationException("Informe ao menos um jogador para a venda.");
        }

        if (fromTeamId == Guid.Empty || toTeamId == Guid.Empty)
        {
            throw new AdminValidationException("Times de origem e destino são obrigatórios.");
        }

        if (fromTeamId == toTeamId)
        {
            throw new AdminValidationException("Origem e destino devem ser diferentes.");
        }

        if (amount < 0)
        {
            throw new AdminValidationException("O valor da venda não pode ser negativo.");
        }

        EnsureReason(reason);

        await EnsureAdminAsync(adminToken, ct).ConfigureAwait(false);

        if (playerIds.Length != playerIds.Distinct().Count())
        {
            throw new AdminValidationException("Há jogadores duplicados na operação.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var fromTeam = await _dbContext.Teams
            .FirstOrDefaultAsync(t => t.TeamId == fromTeamId, ct)
            .ConfigureAwait(false)
            ?? throw new AdminValidationException("Time de origem não encontrado.");

        var toTeam = await _dbContext.Teams
            .FirstOrDefaultAsync(t => t.TeamId == toTeamId, ct)
            .ConfigureAwait(false)
            ?? throw new AdminValidationException("Time de destino não encontrado.");

        var players = await _dbContext.Players
            .Where(p => playerIds.Contains(p.PublicId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (players.Count != playerIds.Length)
        {
            throw new AdminValidationException("Um ou mais jogadores não foram encontrados.");
        }

        foreach (var player in players)
        {
            if (player.CurrentTeamId != fromTeamId)
            {
                throw new AdminValidationException($"O jogador {player.Name} não pertence ao time de origem.");
            }
        }

        await EnsureNoMarketConflictAsync(players.Select(p => p.PlayerId), ct).ConfigureAwait(false);

        var destinationPlayersCount = await _dbContext.Players
            .AsNoTracking()
            .CountAsync(p => p.CurrentTeamId == toTeamId, ct)
            .ConfigureAwait(false);

        if (destinationPlayersCount + players.Count > SquadLimit)
        {
            throw new AdminValidationException($"A operação excederia o limite de {SquadLimit} jogadores no time de destino.");
        }

        var normalizedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

        var availableBudget = toTeam.Budget - toTeam.BudgetBlocked;
        if (availableBudget < normalizedAmount)
        {
            throw new AdminValidationException("O time comprador não possui saldo disponível suficiente.");
        }

        toTeam.Budget -= normalizedAmount;
        fromTeam.Budget += normalizedAmount;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var notes = $"Venda - {fromTeam.TeamName} → {toTeam.TeamName} ({players.Count} jogadores) por R$ {normalizedAmount:0,0.00}. Motivo: {reason}";

        foreach (var player in players)
        {
            player.CurrentTeamId = toTeamId;
            await SyncRosterAsync(player.PlayerId, toTeamId, ct).ConfigureAwait(false);

            var history = new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = TransferType.Sale,
                PlayerId = player.PlayerId,
                PlayerPublicId = player.PublicId,
                FromTeamId = fromTeamId,
                ToTeamId = toTeamId,
                Amount = normalizedAmount,
                Notes = notes,
                PerformedBy = NormalizeToken(adminToken),
                PerformedAtUtc = now
            };

            await _dbContext.TransferHistories.AddAsync(history, ct).ConfigureAwait(false);
        }

        await RegisterAdminLogAsync(2, adminToken, new
        {
            Operation = "Sell",
            FromTeamId = fromTeamId,
            ToTeamId = toTeamId,
            Players = players.Select(p => p.PublicId).ToArray(),
            Amount = normalizedAmount,
            Reason = reason,
            CreatedAtUtc = now
        }, now, ct).ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        var message = $"Operação concluída: venda de {players.Count} jogador(es) do {fromTeam.TeamName} para {toTeam.TeamName} por R$ {normalizedAmount:0,0.00}.";
        return new TransferResult(true, message);
    }

    public async Task<TransferResult> SwapAsync(
        string adminToken,
        Guid teamAId,
        Guid[] playersFromA,
        Guid teamBId,
        Guid[] playersFromB,
        decimal cashAdjustFromAToB,
        string reason,
        CancellationToken ct)
    {
        if (teamAId == Guid.Empty || teamBId == Guid.Empty)
        {
            throw new AdminValidationException("Times da troca são obrigatórios.");
        }

        if (teamAId == teamBId)
        {
            throw new AdminValidationException("Os times envolvidos na troca devem ser diferentes.");
        }

        if (playersFromA is null || playersFromA.Length == 0)
        {
            throw new AdminValidationException("Selecione ao menos um jogador do time A.");
        }

        if (playersFromB is null || playersFromB.Length == 0)
        {
            throw new AdminValidationException("Selecione ao menos um jogador do time B.");
        }

        EnsureReason(reason);
        await EnsureAdminAsync(adminToken, ct).ConfigureAwait(false);

        if (playersFromA.Length != playersFromA.Distinct().Count() || playersFromB.Length != playersFromB.Distinct().Count())
        {
            throw new AdminValidationException("Há jogadores duplicados na operação.");
        }

        if (playersFromA.Intersect(playersFromB).Any())
        {
            throw new AdminValidationException("Um jogador não pode participar duas vezes da mesma troca.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var teamA = await _dbContext.Teams.FirstOrDefaultAsync(t => t.TeamId == teamAId, ct).ConfigureAwait(false)
            ?? throw new AdminValidationException("Time A não encontrado.");
        var teamB = await _dbContext.Teams.FirstOrDefaultAsync(t => t.TeamId == teamBId, ct).ConfigureAwait(false)
            ?? throw new AdminValidationException("Time B não encontrado.");

        var allIds = playersFromA.Concat(playersFromB).ToArray();
        var players = await _dbContext.Players
            .Where(p => allIds.Contains(p.PublicId))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (players.Count != allIds.Length)
        {
            throw new AdminValidationException("Um ou mais jogadores não foram encontrados.");
        }

        foreach (var playerId in playersFromA)
        {
            var player = players.First(p => p.PublicId == playerId);
            if (player.CurrentTeamId != teamAId)
            {
                throw new AdminValidationException($"O jogador {player.Name} não pertence ao time A.");
            }
        }

        foreach (var playerId in playersFromB)
        {
            var player = players.First(p => p.PublicId == playerId);
            if (player.CurrentTeamId != teamBId)
            {
                throw new AdminValidationException($"O jogador {player.Name} não pertence ao time B.");
            }
        }

        await EnsureNoMarketConflictAsync(players.Select(p => p.PlayerId), ct).ConfigureAwait(false);

        var teamAPlayersCount = await _dbContext.Players.AsNoTracking().CountAsync(p => p.CurrentTeamId == teamAId, ct).ConfigureAwait(false);
        var teamBPlayersCount = await _dbContext.Players.AsNoTracking().CountAsync(p => p.CurrentTeamId == teamBId, ct).ConfigureAwait(false);

        var projectedA = teamAPlayersCount - playersFromA.Length + playersFromB.Length;
        var projectedB = teamBPlayersCount - playersFromB.Length + playersFromA.Length;

        if (projectedA > SquadLimit || projectedB > SquadLimit)
        {
            throw new AdminValidationException("A troca ultrapassaria o limite de 23 jogadores em um dos times.");
        }

        var normalizedAdjust = decimal.Round(cashAdjustFromAToB, 2, MidpointRounding.AwayFromZero);

        if (normalizedAdjust > 0)
        {
            var available = teamA.Budget - teamA.BudgetBlocked;
            if (available < normalizedAdjust)
            {
                throw new AdminValidationException("O time A não possui saldo disponível para o ajuste solicitado.");
            }
        }
        else if (normalizedAdjust < 0)
        {
            var amount = Math.Abs(normalizedAdjust);
            var available = teamB.Budget - teamB.BudgetBlocked;
            if (available < amount)
            {
                throw new AdminValidationException("O time B não possui saldo disponível para o ajuste solicitado.");
            }
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (normalizedAdjust > 0)
        {
            teamA.Budget -= normalizedAdjust;
            teamB.Budget += normalizedAdjust;
        }
        else if (normalizedAdjust < 0)
        {
            var value = Math.Abs(normalizedAdjust);
            teamB.Budget -= value;
            teamA.Budget += value;
        }

        foreach (var playerId in playersFromA)
        {
            var player = players.First(p => p.PublicId == playerId);
            player.CurrentTeamId = teamBId;
            await SyncRosterAsync(player.PlayerId, teamBId, ct).ConfigureAwait(false);
        }

        foreach (var playerId in playersFromB)
        {
            var player = players.First(p => p.PublicId == playerId);
            player.CurrentTeamId = teamAId;
            await SyncRosterAsync(player.PlayerId, teamAId, ct).ConfigureAwait(false);
        }

        var liquidText = normalizedAdjust switch
        {
            > 0 => $"Ajuste líquido: +R$ {normalizedAdjust:0,0.00} para {teamB.TeamName}.",
            < 0 => $"Ajuste líquido: +R$ {Math.Abs(normalizedAdjust):0,0.00} para {teamA.TeamName}.",
            _ => "Ajuste líquido: R$ 0,00."
        };

        var notes = $"Troca {teamA.TeamName} ({playersFromA.Length}) ↔ {teamB.TeamName} ({playersFromB.Length}). {liquidText} Motivo: {reason}";
        var performedBy = NormalizeToken(adminToken);

        foreach (var playerId in playersFromA)
        {
            var player = players.First(p => p.PublicId == playerId);
            await _dbContext.TransferHistories.AddAsync(new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = TransferType.Swap,
                PlayerId = player.PlayerId,
                PlayerPublicId = player.PublicId,
                FromTeamId = teamAId,
                ToTeamId = teamBId,
                Amount = null,
                Notes = notes,
                PerformedBy = performedBy,
                PerformedAtUtc = now
            }, ct).ConfigureAwait(false);
        }

        foreach (var playerId in playersFromB)
        {
            var player = players.First(p => p.PublicId == playerId);
            await _dbContext.TransferHistories.AddAsync(new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = TransferType.Swap,
                PlayerId = player.PlayerId,
                PlayerPublicId = player.PublicId,
                FromTeamId = teamBId,
                ToTeamId = teamAId,
                Amount = null,
                Notes = notes,
                PerformedBy = performedBy,
                PerformedAtUtc = now
            }, ct).ConfigureAwait(false);
        }

        await RegisterAdminLogAsync(3, adminToken, new
        {
            Operation = "Swap",
            TeamAId = teamAId,
            TeamBId = teamBId,
            PlayersFromA = playersFromA,
            PlayersFromB = playersFromB,
            CashAdjustFromAToB = normalizedAdjust,
            Reason = reason,
            CreatedAtUtc = now
        }, now, ct).ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        var message = $"Operação concluída: troca entre {teamA.TeamName} e {teamB.TeamName}. {liquidText}";
        return new TransferResult(true, message);
    }

    public async Task<TransferResult> MoveAsync(
        string adminToken,
        Guid playerId,
        Guid toTeamId,
        string reason,
        CancellationToken ct)
    {
        if (playerId == Guid.Empty)
        {
            throw new AdminValidationException("Jogador inválido.");
        }

        if (toTeamId == Guid.Empty)
        {
            throw new AdminValidationException("Time de destino é obrigatório.");
        }

        EnsureReason(reason);
        await EnsureAdminAsync(adminToken, ct).ConfigureAwait(false);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var player = await _dbContext.Players.FirstOrDefaultAsync(p => p.PublicId == playerId, ct).ConfigureAwait(false)
            ?? throw new AdminValidationException("Jogador não encontrado.");

        await EnsureNoMarketConflictAsync(new[] { player.PlayerId }, ct).ConfigureAwait(false);

        var toTeam = await _dbContext.Teams.FirstOrDefaultAsync(t => t.TeamId == toTeamId, ct).ConfigureAwait(false)
            ?? throw new AdminValidationException("Time de destino não encontrado.");

        var destinationPlayers = await _dbContext.Players.AsNoTracking().CountAsync(p => p.CurrentTeamId == toTeamId, ct).ConfigureAwait(false);
        if (destinationPlayers + 1 > SquadLimit)
        {
            throw new AdminValidationException($"A operação excederia o limite de {SquadLimit} jogadores no time de destino.");
        }

        var fromTeamId = player.CurrentTeamId;
        player.CurrentTeamId = toTeamId;
        await SyncRosterAsync(player.PlayerId, toTeamId, ct).ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var performedBy = NormalizeToken(adminToken);

        await _dbContext.TransferHistories.AddAsync(new TransferHistory
        {
            TransferId = Guid.NewGuid(),
            Type = TransferType.AdminMove,
            PlayerId = player.PlayerId,
            PlayerPublicId = player.PublicId,
            FromTeamId = fromTeamId,
            ToTeamId = toTeamId,
            Amount = null,
            Notes = $"Movimentação administrativa: {reason}",
            PerformedBy = performedBy,
            PerformedAtUtc = now
        }, ct).ConfigureAwait(false);

        await RegisterAdminLogAsync(4, adminToken, new
        {
            Operation = "Move",
            PlayerId = player.PublicId,
            FromTeamId = fromTeamId,
            ToTeamId = toTeamId,
            Reason = reason,
            CreatedAtUtc = now
        }, now, ct).ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        var message = $"Operação concluída: jogador {player.Name} movido para {toTeam.TeamName}.";
        return new TransferResult(true, message);
    }

    public async Task<AdjustBudgetResult> AdjustBudgetAsync(
        string adminToken,
        Guid teamId,
        decimal delta,
        string reason,
        CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new AdminValidationException("Time é obrigatório.");
        }

        var normalizedDelta = decimal.Round(delta, 2, MidpointRounding.AwayFromZero);

        if (normalizedDelta == 0m)
        {
            throw new AdminValidationException("O ajuste deve ser diferente de zero.");
        }

        EnsureReason(reason);
        await EnsureAdminAsync(adminToken, ct).ConfigureAwait(false);

        var team = await _dbContext.Teams.FirstOrDefaultAsync(t => t.TeamId == teamId, ct).ConfigureAwait(false)
            ?? throw new AdminValidationException("Time não encontrado.");

        if (normalizedDelta > 0)
        {
            await _budgetService.RegistrarAjusteAsync(teamId, normalizedDelta, "ADMIN", reason, true, ct).ConfigureAwait(false);
        }
        else
        {
            await _budgetService.RegistrarAjusteAsync(teamId, Math.Abs(normalizedDelta), "ADMIN", reason, false, ct).ConfigureAwait(false);
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await RegisterAdminLogAsync(0, adminToken, new
        {
            Operation = "AdjustBudget",
            TeamId = teamId,
            Delta = normalizedDelta,
            Reason = reason,
            CreatedAtUtc = now
        }, now, ct).ConfigureAwait(false);

        await _dbContext.Entry(team).ReloadAsync(ct).ConfigureAwait(false);
        var message = normalizedDelta > 0
            ? $"Ajuste concluído: crédito de R$ {normalizedDelta:0,0.00} para {team.TeamName}."
            : $"Ajuste concluído: débito de R$ {Math.Abs(normalizedDelta):0,0.00} para {team.TeamName}.";

        return new AdjustBudgetResult(true, message, team.Budget);
    }

    public async Task<CancelItemResult> CancelMarketItemAsync(
        string adminToken,
        Guid itemId,
        string reason,
        CancellationToken ct)
    {
        if (itemId == Guid.Empty)
        {
            throw new AdminValidationException("Item inválido.");
        }

        EnsureReason(reason);
        await EnsureAdminAsync(adminToken, ct).ConfigureAwait(false);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var item = await _dbContext.MarketItems
            .Include(i => i.Player)
            .FirstOrDefaultAsync(i => i.ItemId == itemId, ct)
            .ConfigureAwait(false)
            ?? throw new AdminValidationException("Item de mercado não encontrado.");

        if (item.Status is MarketItemStatus.BuyNow or MarketItemStatus.Completed)
        {
            throw new AdminValidationException("Itens concluídos não podem ser cancelados.");
        }

        if (item.Status == MarketItemStatus.LeaderChanged && item.CurrentLeaderTeamId.HasValue)
        {
            throw new AdminValidationException("Não é possível cancelar um item que possui líder atual.");
        }

        if (item.Status == MarketItemStatus.Active && item.CurrentLeaderTeamId.HasValue)
        {
            throw new AdminValidationException("Não é possível cancelar um item que possui líder atual.");
        }

        if (item.Status == MarketItemStatus.Cancelled)
        {
            return new CancelItemResult(true, "Item já estava cancelado.");
        }

        if (item.CurrentLeaderTeamId.HasValue && item.CurrentLeaderAmount.HasValue)
        {
            var team = await _dbContext.Teams.FirstOrDefaultAsync(t => t.TeamId == item.CurrentLeaderTeamId, ct).ConfigureAwait(false);
            if (team is not null)
            {
                team.BudgetBlocked = Math.Max(0m, team.BudgetBlocked - item.CurrentLeaderAmount.Value);
            }
        }

        item.Status = MarketItemStatus.Cancelled;
        item.LastUpdateUtc = now;
        item.CurrentLeaderTeamId = null;
        item.CurrentLeaderAmount = null;

        await RegisterAdminLogAsync(1, adminToken, new
        {
            Operation = "CancelMarketItem",
            ItemId = itemId,
            Reason = reason,
            CreatedAtUtc = now
        }, now, ct).ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        return new CancelItemResult(true, "Item de mercado cancelado com sucesso.");
    }

    private static void EnsureReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AdminValidationException("Informe um motivo para a operação.");
        }
    }

    private async Task EnsureAdminAsync(string token, CancellationToken ct)
    {
        var normalized = NormalizeToken(token);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new AdminForbiddenException("Token de administrador inválido.");
        }

        if (!Guid.TryParse(normalized, out var tokenGuid))
        {
            throw new AdminForbiddenException("Token de administrador inválido.");
        }

        var exists = await _dbContext.AdminTokens
            .AsNoTracking()
            .AnyAsync(t => t.Token == tokenGuid, ct)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new AdminForbiddenException("Token de administrador inválido.");
        }
    }

    private async Task EnsureNoMarketConflictAsync(IEnumerable<int> playerIds, CancellationToken ct)
    {
        var ids = playerIds.ToArray();
        var conflict = await _dbContext.MarketItems
            .AsNoTracking()
            .Where(i => ids.Contains(i.PlayerId) && (i.Status == MarketItemStatus.Active || i.Status == MarketItemStatus.LeaderChanged))
            .Select(i => i.PlayerId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (conflict != 0)
        {
            throw new AdminConflictException("Um ou mais jogadores estão listados no mercado ativo. Cancele o item antes de prosseguir.");
        }
    }

    private async Task SyncRosterAsync(int playerId, Guid? newTeamId, CancellationToken ct)
    {
        var entries = await _dbContext.TeamRosters
            .Where(r => r.PlayerId == playerId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var entry in entries)
        {
            if (newTeamId is null || entry.TeamId != newTeamId.Value)
            {
                _dbContext.TeamRosters.Remove(entry);
            }
        }

        if (newTeamId.HasValue && entries.All(e => e.TeamId != newTeamId.Value))
        {
            await _dbContext.TeamRosters.AddAsync(new TeamRoster
            {
                PlayerId = playerId,
                TeamId = newTeamId.Value
            }, ct).ConfigureAwait(false);
        }
    }

    private async Task RegisterAdminLogAsync(int actionType, string adminToken, object payload, DateTime createdAtUtc, CancellationToken ct)
    {
        var normalized = NormalizeToken(adminToken);

        var log = new AdminActionsLog
        {
            ActionId = Guid.NewGuid(),
            ActionType = actionType,
            PerformedBy = normalized,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAtUtc = createdAtUtc
        };

        await _dbContext.AdminActionsLogs.AddAsync(log, ct).ConfigureAwait(false);
    }

    private static string NormalizeToken(string token) => string.IsNullOrWhiteSpace(token) ? string.Empty : token.Trim();
}
