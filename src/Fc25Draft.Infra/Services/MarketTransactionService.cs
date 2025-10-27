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

    public MarketTransactionService(DraftDbContext dbContext, IPricingService pricingService, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _pricingService = pricingService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<TransferMarketItem> PlaceBidAsync(Guid marketItemId, Guid teamId, decimal bidValue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var roundedBid = _pricingService.RoundToTenth(bidValue);
        if (roundedBid <= 0)
        {
            throw new MarketBidBelowMinimumException("O valor do lance deve ser maior que zero.");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        var item = await _dbContext.TransferMarketItems
            .Include(i => i.Player)
            .Include(i => i.MaiorLanceTeam)
            .FirstOrDefaultAsync(i => i.MarketItemId == marketItemId, ct)
            .ConfigureAwait(false);

        if (item is null || !string.Equals(item.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            throw new MarketItemNotFoundException("Item de mercado não encontrado ou indisponível.");
        }

        var originalStatus = item.Status;
        var originalLanceAtual = item.LanceAtual;
        var originalMaiorLanceTeamId = item.MaiorLanceTeamId;

        if (originalMaiorLanceTeamId.HasValue && originalMaiorLanceTeamId.Value == teamId)
        {
            throw new MarketTeamAlreadyLeadingException("Seu time já lidera este item.");
        }

        var pricing = await _pricingService.CalculateForPlayerAsync(item.PlayerId, ct).ConfigureAwait(false);
        var lanceInicial = pricing.LanceInicial;
        var baseValor = originalLanceAtual ?? lanceInicial;
        var incrementoMinimo = originalLanceAtual.HasValue
            ? _pricingService.NextMinIncrement(originalLanceAtual.Value)
            : _pricingService.NextMinIncrement(0m);
        var lanceMinimo = _pricingService.RoundToTenth(baseValor + incrementoMinimo);

        if (roundedBid < lanceMinimo)
        {
            throw new MarketBidBelowMinimumException($"Lance mínimo permitido: {lanceMinimo:0.0}.");
        }

        var saldoDisponivel = await GetSaldoDisponivelAsync(teamId, ct).ConfigureAwait(false);
        if (saldoDisponivel < roundedBid)
        {
            throw new MarketInsufficientBalanceException("Saldo insuficiente para realizar o lance.");
        }

        await EnsureStateUnchangedAsync(marketItemId, originalStatus, originalMaiorLanceTeamId, originalLanceAtual, ct).ConfigureAwait(false);

        item.LanceAtual = roundedBid;
        item.MaiorLanceTeamId = teamId;

        var bid = new Bid
        {
            BidId = Guid.NewGuid(),
            MarketItemId = marketItemId,
            TeamId = teamId,
            Valor = roundedBid,
            DataUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _dbContext.Bids.AddAsync(bid, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        await _dbContext.Entry(item).Reference(i => i.Player).LoadAsync(ct).ConfigureAwait(false);
        await _dbContext.Entry(item).Reference(i => i.MaiorLanceTeam).LoadAsync(ct).ConfigureAwait(false);
        await _dbContext.Entry(item).Reference(i => i.VencedorTeam).LoadAsync(ct).ConfigureAwait(false);

        return item;
    }

    public async Task<TransferMarketItem> BuyNowAsync(Guid marketItemId, Guid teamId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

        var item = await _dbContext.TransferMarketItems
            .Include(i => i.Player)
            .FirstOrDefaultAsync(i => i.MarketItemId == marketItemId, ct)
            .ConfigureAwait(false);

        if (item is null || !string.Equals(item.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            throw new MarketItemNotFoundException("Item de mercado não encontrado ou indisponível.");
        }

        var originalStatus = item.Status;
        var originalLanceAtual = item.LanceAtual;
        var originalMaiorLanceTeamId = item.MaiorLanceTeamId;

        var jogadorNoElenco = await _dbContext.TeamRosters
            .AsNoTracking()
            .AnyAsync(r => r.PlayerId == item.PlayerId, ct)
            .ConfigureAwait(false);

        if (jogadorNoElenco)
        {
            throw new MarketPlayerUnavailableException("O jogador já está vinculado a um elenco.");
        }

        var saldoDisponivel = await GetSaldoDisponivelAsync(teamId, ct).ConfigureAwait(false);
        if (saldoDisponivel < item.PrecoComprarAgora)
        {
            throw new MarketInsufficientBalanceException("Saldo insuficiente para comprar agora.");
        }

        await EnsureStateUnchangedAsync(marketItemId, originalStatus, originalMaiorLanceTeamId, originalLanceAtual, ct).ConfigureAwait(false);

        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;

        item.Status = "SOLD";
        item.DataFimUtc = nowUtc;
        item.VencedorTeamId = teamId;
        item.MaiorLanceTeamId = teamId;
        item.LanceAtual = item.PrecoComprarAgora;

        var budget = await _dbContext.TeamBudgets.FirstOrDefaultAsync(b => b.TeamId == teamId, ct).ConfigureAwait(false)
                     ?? throw new MarketInsufficientBalanceException("Saldo do time não encontrado.");

        budget.Saldo -= item.PrecoComprarAgora;

        var history = new TransferHistory
        {
            TransferHistoryId = Guid.NewGuid(),
            PlayerId = item.PlayerId,
            OrigemTeamId = null,
            DestinoTeamId = teamId,
            Valor = item.PrecoComprarAgora,
            Tipo = "MARKET_AUCTION",
            DataUtc = nowUtc,
            Observacao = null
        };

        var rosterEntry = new TeamRoster
        {
            TeamId = teamId,
            PlayerId = item.PlayerId
        };

        await _dbContext.TransferHistories.AddAsync(history, ct).ConfigureAwait(false);
        await _dbContext.TeamRosters.AddAsync(rosterEntry, ct).ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        await _dbContext.Entry(item).Reference(i => i.Player).LoadAsync(ct).ConfigureAwait(false);
        await _dbContext.Entry(item).Reference(i => i.MaiorLanceTeam).LoadAsync(ct).ConfigureAwait(false);
        await _dbContext.Entry(item).Reference(i => i.VencedorTeam).LoadAsync(ct).ConfigureAwait(false);

        return item;
    }

    private async Task<decimal> GetSaldoDisponivelAsync(Guid teamId, CancellationToken ct)
    {
        var budgetEntry = await _dbContext.TeamBudgets
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.TeamId == teamId, ct)
            .ConfigureAwait(false);

        if (budgetEntry is null)
        {
            throw new MarketInsufficientBalanceException("Saldo do time não encontrado.");
        }

        var budget = budgetEntry.Saldo;

        var bloqueado = await _dbContext.TransferMarketItems
            .Where(m => m.Status == "OPEN" && m.MaiorLanceTeamId == teamId && m.LanceAtual != null)
            .SumAsync(m => (decimal?)m.LanceAtual!)
            .ConfigureAwait(false) ?? 0m;

        return budget - bloqueado;
    }

    private async Task EnsureStateUnchangedAsync(Guid marketItemId, string expectedStatus, Guid? expectedTeamId, decimal? expectedBid, CancellationToken ct)
    {
        var snapshot = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Where(i => i.MarketItemId == marketItemId)
            .Select(i => new { i.Status, i.MaiorLanceTeamId, i.LanceAtual })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (snapshot is null || !string.Equals(snapshot.Status, "OPEN", StringComparison.OrdinalIgnoreCase))
        {
            throw new MarketStateChangedException("O item não está mais disponível.");
        }

        var sameTeam = snapshot.MaiorLanceTeamId == expectedTeamId;
        var sameBid = NullableDecimalEquals(snapshot.LanceAtual, expectedBid);
        var sameStatus = string.Equals(snapshot.Status, expectedStatus, StringComparison.OrdinalIgnoreCase);

        if (!sameStatus || !sameTeam || !sameBid)
        {
            throw new MarketStateChangedException("O estado do item foi alterado. Tente novamente.");
        }
    }

    private static bool NullableDecimalEquals(decimal? left, decimal? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return decimal.Compare(left.Value, right.Value) == 0;
    }
}
