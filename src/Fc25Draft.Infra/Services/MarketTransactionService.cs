using System.Data;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class MarketTransactionService : IMarketTransactionService
{
    private readonly DraftDbContext _dbContext;
    private readonly IPricingService _pricingService;
    private readonly TimeProvider _timeProvider;

    public MarketTransactionService(
        DraftDbContext dbContext,
        IPricingService pricingService,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _pricingService = pricingService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TransferMarketItem> PlaceBidAsync(Guid marketItemId, Guid teamId, decimal bidValue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct)
            .ConfigureAwait(false);

        var item = await LoadItemForUpdateAsync(marketItemId, ct).ConfigureAwait(false);
        if (item is null || !string.Equals(item.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            throw new TransferMarketNotFoundException("Item de mercado não encontrado ou indisponível.");
        }

        var roundedBid = _pricingService.RoundToTenth(bidValue);
        if (roundedBid <= 0m)
        {
            throw new TransferMarketValidationException("O valor do lance deve ser maior que zero.");
        }

        if (item.MaiorLanceTeamId == teamId)
        {
            throw new TransferMarketConflictException("Seu time já lidera este item.");
        }

        var minimumBid = await CalculateMinimumBidAsync(item, ct).ConfigureAwait(false);
        if (roundedBid < minimumBid)
        {
            throw new TransferMarketValidationException($"O lance mínimo permitido é {minimumBid:0.0}.");
        }

        var saldoDisponivel = await GetSaldoDisponivelAsync(teamId, ct).ConfigureAwait(false);
        if (saldoDisponivel < roundedBid)
        {
            throw new TransferMarketConflictException("Saldo insuficiente para registrar o lance.");
        }

        await EnsureStateUnchangedAsync(item.MarketItemId, item.Status, item.MaiorLanceTeamId, item.LanceAtual, ct)
            .ConfigureAwait(false);

        item.LanceAtual = roundedBid;
        item.MaiorLanceTeamId = teamId;

        var bid = new Bid
        {
            BidId = Guid.NewGuid(),
            MarketItemId = item.MarketItemId,
            TeamId = teamId,
            Valor = roundedBid,
            DataUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.Bids.Add(bid);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        return await LoadItemForResponseAsync(marketItemId, ct).ConfigureAwait(false);
    }

    public async Task<TransferMarketItem> BuyNowAsync(Guid marketItemId, Guid teamId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await using var transaction = await _dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, ct)
            .ConfigureAwait(false);

        var item = await LoadItemForUpdateAsync(marketItemId, ct).ConfigureAwait(false);
        if (item is null || !string.Equals(item.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            throw new TransferMarketNotFoundException("Item de mercado não encontrado ou indisponível.");
        }

        var finalPrice = _pricingService.RoundToTenth(item.PrecoComprarAgora);

        var saldoDisponivel = await GetSaldoDisponivelAsync(teamId, ct).ConfigureAwait(false);
        if (saldoDisponivel < finalPrice)
        {
            throw new TransferMarketConflictException("Saldo insuficiente para comprar agora.");
        }

        var playerEmOutroTime = await _dbContext.TeamRosters
            .AsNoTracking()
            .AnyAsync(r => r.PlayerId == item.PlayerId, ct)
            .ConfigureAwait(false);
        if (playerEmOutroTime)
        {
            throw new TransferMarketConflictException("Jogador já está vinculado a outro elenco.");
        }

        await EnsureStateUnchangedAsync(item.MarketItemId, item.Status, item.MaiorLanceTeamId, item.LanceAtual, ct)
            .ConfigureAwait(false);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        item.Status = "SOLD";
        item.DataFimUtc = nowUtc;
        item.VencedorTeamId = teamId;
        item.MaiorLanceTeamId = teamId;
        item.LanceAtual = finalPrice;

        await DebitTeamBudgetAsync(teamId, finalPrice, ct).ConfigureAwait(false);

        var transferHistory = new TransferHistory
        {
            TransferHistoryId = Guid.NewGuid(),
            PlayerId = item.PlayerId,
            OrigemTeamId = null,
            DestinoTeamId = teamId,
            Valor = finalPrice,
            Tipo = "MARKET_AUCTION",
            DataUtc = nowUtc
        };

        var rosterEntry = new TeamRoster
        {
            TeamId = teamId,
            PlayerId = item.PlayerId
        };

        _dbContext.TransferHistories.Add(transferHistory);
        _dbContext.TeamRosters.Add(rosterEntry);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        return await LoadItemForResponseAsync(marketItemId, ct).ConfigureAwait(false);
    }

    private async Task<TransferMarketItem?> LoadItemForUpdateAsync(Guid marketItemId, CancellationToken ct)
    {
        return await _dbContext.TransferMarketItems
            .Include(i => i.Player)
            .FirstOrDefaultAsync(i => i.MarketItemId == marketItemId, ct)
            .ConfigureAwait(false);
    }

    private async Task<TransferMarketItem> LoadItemForResponseAsync(Guid marketItemId, CancellationToken ct)
    {
        var item = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Include(i => i.Player)
            .ThenInclude(p => p.Position)
            .Include(i => i.MaiorLanceTeam)
            .Include(i => i.VencedorTeam)
            .FirstOrDefaultAsync(i => i.MarketItemId == marketItemId, ct)
            .ConfigureAwait(false);

        if (item is null)
        {
            throw new TransferMarketNotFoundException("Item de mercado não encontrado após a operação.");
        }

        return item;
    }

    private async Task<decimal> CalculateMinimumBidAsync(TransferMarketItem item, CancellationToken ct)
    {
        if (item.LanceAtual.HasValue)
        {
            var increment = _pricingService.NextMinIncrement(item.LanceAtual.Value);
            return _pricingService.RoundToTenth(item.LanceAtual.Value + increment);
        }

        var pricing = await _pricingService.CalculateForPlayerAsync(item.PlayerId, ct).ConfigureAwait(false);
        var baseValue = pricing.LanceInicial;
        var incrementWhenNoBid = _pricingService.NextMinIncrement(0m);
        return _pricingService.RoundToTenth(baseValue + incrementWhenNoBid);
    }

    private async Task EnsureStateUnchangedAsync(
        Guid marketItemId,
        string status,
        Guid? maiorLanceTeamId,
        decimal? lanceAtual,
        CancellationToken ct)
    {
        var snapshot = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Where(i => i.MarketItemId == marketItemId)
            .Select(i => new { i.Status, i.MaiorLanceTeamId, i.LanceAtual })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (snapshot is null)
        {
            throw new TransferMarketNotFoundException("Item de mercado não encontrado.");
        }

        var statusChanged = !string.Equals(snapshot.Status, status, StringComparison.OrdinalIgnoreCase);
        var leaderChanged = snapshot.MaiorLanceTeamId != maiorLanceTeamId;
        var bidChanged = snapshot.LanceAtual != lanceAtual;

        if (statusChanged || leaderChanged || bidChanged)
        {
            throw new TransferMarketConflictException("O estado do item foi alterado. Tente novamente.");
        }
    }

    private async Task<decimal> GetSaldoDisponivelAsync(Guid teamId, CancellationToken ct)
    {
        var saldo = await _dbContext.TeamBudgets
            .AsNoTracking()
            .Where(b => b.TeamId == teamId)
            .Select(b => (decimal?)b.Saldo)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false) ?? 0m;

        var bloqueado = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Where(m => m.Status == "OPEN" && m.MaiorLanceTeamId == teamId && m.LanceAtual != null)
            .SumAsync(m => (decimal?)m.LanceAtual!, ct)
            .ConfigureAwait(false) ?? 0m;

        return saldo - bloqueado;
    }

    private async Task DebitTeamBudgetAsync(Guid teamId, decimal value, CancellationToken ct)
    {
        var budget = await _dbContext.TeamBudgets
            .FirstOrDefaultAsync(b => b.TeamId == teamId, ct)
            .ConfigureAwait(false);

        if (budget is null)
        {
            throw new InvalidOperationException("Saldo do time não encontrado.");
        }

        budget.Saldo -= value;
    }
}
