using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fc25Draft.Infra.Services;

public class QuickSellService : IQuickSellService
{
    private readonly DraftDbContext _dbContext;
    private readonly IPricingService _pricingService;
    private readonly TimeProvider _timeProvider;

    public QuickSellService(
        DraftDbContext dbContext,
        IPricingService pricingService,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<QuickSellResultDto> QuickSellAsync(Guid teamId, int playerId, string teamToken, CancellationToken ct = default)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("teamId é obrigatório.", nameof(teamId));
        }

        if (playerId <= 0)
        {
            throw new ArgumentException("playerId deve ser positivo.", nameof(playerId));
        }

        var normalizedToken = NormalizeToken(teamToken);

        var team = await _dbContext.Teams
            .Include(t => t.Roster)
                .ThenInclude(r => r.Player)
            .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Time {teamId} não encontrado.");

        var storedToken = NormalizeToken(team.Token);
        if (!string.Equals(storedToken, normalizedToken, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Token inválido para o time informado.");
        }

        var rosterEntry = team.Roster.FirstOrDefault(r => r.PlayerId == playerId);
        if (rosterEntry is null)
        {
            throw new KeyNotFoundException($"Jogador {playerId} não encontrado no elenco do time.");
        }

        var rosterCount = team.Roster.Count;
        if (rosterCount - 1 < 18)
        {
            throw new InvalidOperationException("O time ficaria abaixo do mínimo de 18 jogadores.");
        }

        var player = rosterEntry.Player ?? await _dbContext.Players
            .Include(p => p.TeamRosters)
            .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Jogador {playerId} não encontrado.");

        if (player.Status != PlayerStatus.Active)
        {
            throw new InvalidOperationException("Somente jogadores ativos podem ser negociados.");
        }

        if (player.CurrentTeamId != teamId)
        {
            throw new InvalidOperationException("Jogador não está associado ao time informado.");
        }

        var pricing = await _pricingService.CalculateForPlayerAsync(player.PlayerId, ct).ConfigureAwait(false);
        var basePrice = pricing.BasePrice;
        var payout = decimal.Round(basePrice * 0.8m, 2, MidpointRounding.AwayFromZero);
        var occurredAt = _timeProvider.GetUtcNow().UtcDateTime;

        var oldOverall = player.Overall;
        var newOverall = CalculateOverallBump(player);

        player.PreviousOverall = oldOverall;
        player.Overall = newOverall;
        player.Status = PlayerStatus.FreeAgent;
        player.CurrentTeamId = null;

        _dbContext.TeamRosters.Remove(rosterEntry);
        if (player.TeamRosters.Contains(rosterEntry))
        {
            player.TeamRosters.Remove(rosterEntry);
        }

        if (team.Roster.Contains(rosterEntry))
        {
            team.Roster.Remove(rosterEntry);
        }

        team.Budget = decimal.Round(team.Budget + payout, 2, MidpointRounding.AwayFromZero);

        var ledgerEntry = new BudgetLedger
        {
            BudgetLedgerId = Guid.NewGuid(),
            TeamId = team.TeamId,
            DataUtc = occurredAt,
            Tipo = "CREDIT",
            Origem = "QUICKSELL",
            Valor = payout,
            Descricao = $"Quick sell de {player.Name}"
        };

        var historyEntry = new TransferHistory
        {
            TransferId = Guid.NewGuid(),
            Type = TransferType.QuickSell,
            PlayerId = player.PlayerId,
            FromTeamId = team.TeamId,
            ToTeamId = null,
            Amount = payout,
            Payout = payout,
            OldOverall = oldOverall,
            NewOverall = newOverall,
            Notes = $"Quick sell executado automaticamente. Overall: {oldOverall} → {newOverall}.",
            PerformedBy = team.TeamName,
            OccurredAtUtc = occurredAt
        };

        var useTransaction = _dbContext.Database.IsRelational();
        IDbContextTransaction? transaction = null;

        if (useTransaction)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        }

        try
        {
            await _dbContext.BudgetLedgers.AddAsync(ledgerEntry, ct).ConfigureAwait(false);
            await _dbContext.TransferHistories.AddAsync(historyEntry, ct).ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct).ConfigureAwait(false);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
            }

            throw;
        }

        return new QuickSellResultDto(
            team.TeamId,
            team.TeamName,
            player.PlayerId,
            player.PlayerGuid,
            player.Name,
            oldOverall,
            newOverall,
            player.Status,
            basePrice,
            payout,
            team.Budget,
            occurredAt);
    }

    internal static int CalculateOverallBump(Player player)
    {
        if (player is null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        var seed = HashCode.Combine(player.PlayerId, player.PlayerGuid);
        var bump = Math.Abs(seed % 4) + 1;
        var updated = player.Overall + bump;
        return Math.Min(99, updated);
    }

    private static string NormalizeToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token de time é obrigatório.", nameof(token));
        }

        return token.Trim().ToUpperInvariant();
    }
}
