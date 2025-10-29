using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Fc25Draft.Infra.Services;

public class MarketSyncService : IMarketSyncService
{
    private const string SystemActor = "sistema";
    private const string LedgerOriginMarket = "MERCADO";
    private const string LedgerOriginTransfer = "TRANSFERENCIA";
    private const string LedgerOriginTrade = "TROCA";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly DraftDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public MarketSyncService(DraftDbContext dbContext, TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task ApplyWinningBidAsync(Guid itemId)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentException("ItemId inválido.", nameof(itemId));
        }

        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;

        if (ownsTransaction)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
        }

        try
        {
            var item = await _dbContext.MarketItems
                .Include(i => i.Player)
                .FirstOrDefaultAsync(i => i.ItemId == itemId)
                .ConfigureAwait(false)
                ?? throw new MarketSyncException("Item de mercado não encontrado.");

            if (!item.CurrentLeaderTeamId.HasValue || !item.CurrentLeaderAmount.HasValue)
            {
                throw new MarketSyncException("O item não possui um vencedor definido.");
            }

            var team = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.TeamId == item.CurrentLeaderTeamId.Value)
                .ConfigureAwait(false)
                ?? throw new MarketSyncException("Time vencedor não encontrado.");

            var amount = decimal.Round(item.CurrentLeaderAmount.Value, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0m)
            {
                throw new MarketSyncException("Valor do lance vencedor inválido.");
            }

            if (team.Budget < amount)
            {
                throw new MarketSyncException("Orçamento insuficiente para concluir o leilão.");
            }

            var player = item.Player ?? await _dbContext.Players
                .FirstOrDefaultAsync(p => p.PlayerId == item.PlayerId)
                .ConfigureAwait(false)
                ?? throw new MarketSyncException("Jogador vinculado ao item não encontrado.");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var previousTeamId = player.CurrentTeamId;

            team.Budget = decimal.Round(team.Budget - amount, 2, MidpointRounding.AwayFromZero);
            team.BudgetBlocked = decimal.Round(Math.Max(0m, team.BudgetBlocked - amount), 2, MidpointRounding.AwayFromZero);

            player.CurrentTeamId = team.TeamId;
            await SyncRosterAsync(team.TeamId, player.PlayerId).ConfigureAwait(false);

            item.WinnerTeamId = team.TeamId;
            item.Status = MarketItemStatus.Completed;
            item.LastUpdateUtc = now;
            item.ExpiresAtUtc = now;

            var history = new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = TransferType.MarketAuction,
                PlayerId = player.PlayerId,
                FromTeamId = previousTeamId,
                ToTeamId = team.TeamId,
                Amount = amount,
                Notes = "Leilão encerrado",
                PerformedBy = SystemActor,
                PerformedAtUtc = now
            };

            await _dbContext.TransferHistories.AddAsync(history).ConfigureAwait(false);

            var ledgerEntry = new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = team.TeamId,
                DataUtc = now,
                Tipo = "DEBIT",
                Origem = LedgerOriginMarket,
                Valor = amount,
                Descricao = $"Leilão do jogador {player.Name}"
            };

            await _dbContext.BudgetLedgers.AddAsync(ledgerEntry).ConfigureAwait(false);

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.MarketAuctionClosed,
                PerformedBy = SystemActor,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    itemId,
                    playerId = player.PlayerId,
                    winnerTeamId = team.TeamId,
                    amount
                }, JsonOptions),
                CreatedAtUtc = now
            };

            await _dbContext.AdminActionsLogs.AddAsync(logEntry).ConfigureAwait(false);

            await _dbContext.SaveChangesAsync().ConfigureAwait(false);

            if (ownsTransaction && transaction is not null)
            {
                await transaction.CommitAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            if (ownsTransaction && transaction is not null)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (ownsTransaction && transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task ApplyTeamSaleAsync(int playerId, Guid fromTeamId, Guid toTeamId, decimal amount)
    {
        if (playerId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerId), "Jogador inválido.");
        }

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
            throw new MarketSyncException("Os times de origem e destino devem ser diferentes.");
        }

        if (amount <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "O valor da venda deve ser maior que zero.");
        }

        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;

        if (ownsTransaction)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
        }

        try
        {
            var player = await _dbContext.Players
                .FirstOrDefaultAsync(p => p.PlayerId == playerId)
                .ConfigureAwait(false)
                ?? throw new MarketSyncException("Jogador não encontrado.");

            if (player.CurrentTeamId != fromTeamId)
            {
                throw new MarketSyncException("O jogador não pertence ao time de origem informado.");
            }

            var seller = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.TeamId == fromTeamId)
                .ConfigureAwait(false)
                ?? throw new MarketSyncException("Time de origem não encontrado.");

            var buyer = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.TeamId == toTeamId)
                .ConfigureAwait(false)
                ?? throw new MarketSyncException("Time de destino não encontrado.");

            var normalizedAmount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
            if (buyer.Budget < normalizedAmount)
            {
                throw new MarketSyncException("Orçamento insuficiente para concluir a venda.");
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var culture = CultureInfo.GetCultureInfo("pt-BR");
            var formattedAmount = normalizedAmount.ToString("N2", culture);

            buyer.Budget = decimal.Round(buyer.Budget - normalizedAmount, 2, MidpointRounding.AwayFromZero);
            seller.Budget = decimal.Round(seller.Budget + normalizedAmount, 2, MidpointRounding.AwayFromZero);

            player.CurrentTeamId = buyer.TeamId;
            await SyncRosterAsync(buyer.TeamId, player.PlayerId).ConfigureAwait(false);

            var history = new TransferHistory
            {
                TransferId = Guid.NewGuid(),
                Type = TransferType.TeamSale,
                PlayerId = player.PlayerId,
                FromTeamId = seller.TeamId,
                ToTeamId = buyer.TeamId,
                Amount = normalizedAmount,
                Notes = $"Venda direta por R${formattedAmount}",
                PerformedBy = SystemActor,
                PerformedAtUtc = now
            };

            await _dbContext.TransferHistories.AddAsync(history).ConfigureAwait(false);

            var debit = new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = buyer.TeamId,
                DataUtc = now,
                Tipo = "DEBIT",
                Origem = LedgerOriginTransfer,
                Valor = normalizedAmount,
                Descricao = $"Compra do jogador {player.Name}"
            };

            var credit = new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = seller.TeamId,
                DataUtc = now,
                Tipo = "CREDIT",
                Origem = LedgerOriginTransfer,
                Valor = normalizedAmount,
                Descricao = $"Venda do jogador {player.Name}"
            };

            await _dbContext.BudgetLedgers.AddRangeAsync(new[] { debit, credit }).ConfigureAwait(false);

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.MarketTeamSale,
                PerformedBy = SystemActor,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    playerId,
                    fromTeamId = seller.TeamId,
                    toTeamId = buyer.TeamId,
                    amount = normalizedAmount
                }, JsonOptions),
                CreatedAtUtc = now
            };

            await _dbContext.AdminActionsLogs.AddAsync(logEntry).ConfigureAwait(false);

            await _dbContext.SaveChangesAsync().ConfigureAwait(false);

            if (ownsTransaction && transaction is not null)
            {
                await transaction.CommitAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            if (ownsTransaction && transaction is not null)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (ownsTransaction && transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task ApplyTeamTradeAsync(int playerIdA, Guid teamA, int playerIdB, Guid teamB, decimal? balance = null)
    {
        if (playerIdA <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerIdA), "Jogador A inválido.");
        }

        if (playerIdB <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(playerIdB), "Jogador B inválido.");
        }

        if (teamA == Guid.Empty)
        {
            throw new ArgumentException("Time A inválido.", nameof(teamA));
        }

        if (teamB == Guid.Empty)
        {
            throw new ArgumentException("Time B inválido.", nameof(teamB));
        }

        if (teamA == teamB)
        {
            throw new MarketSyncException("Os times envolvidos na troca devem ser diferentes.");
        }

        var ownsTransaction = _dbContext.Database.CurrentTransaction is null;
        IDbContextTransaction? transaction = null;

        if (ownsTransaction)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync().ConfigureAwait(false);
        }

        try
        {
            var playerA = await _dbContext.Players
                .FirstOrDefaultAsync(p => p.PlayerId == playerIdA)
                .ConfigureAwait(false)
                ?? throw new MarketSyncException("Jogador A não encontrado.");

            var playerB = await _dbContext.Players
                .FirstOrDefaultAsync(p => p.PlayerId == playerIdB)
                .ConfigureAwait(false)
                ?? throw new MarketSyncException("Jogador B não encontrado.");

            if (playerA.CurrentTeamId != teamA)
            {
                throw new MarketSyncException("Jogador A não pertence ao time informado.");
            }

            if (playerB.CurrentTeamId != teamB)
            {
                throw new MarketSyncException("Jogador B não pertence ao time informado.");
            }

            var sourceTeamA = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.TeamId == teamA)
                .ConfigureAwait(false)
                ?? throw new MarketSyncException("Time A não encontrado.");

            var sourceTeamB = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.TeamId == teamB)
                .ConfigureAwait(false)
                ?? throw new MarketSyncException("Time B não encontrado.");

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var normalizedBalance = balance.HasValue
                ? decimal.Round(balance.Value, 2, MidpointRounding.AwayFromZero)
                : (decimal?)null;

            var ledgerEntries = new List<BudgetLedger>();

            if (normalizedBalance.HasValue && normalizedBalance.Value != 0m)
            {
                var description = $"Troca entre {sourceTeamA.TeamName} e {sourceTeamB.TeamName}";
                if (normalizedBalance.Value > 0m)
                {
                    if (sourceTeamA.Budget < normalizedBalance.Value)
                    {
                        throw new MarketSyncException("O time A não possui orçamento suficiente para o ajuste da troca.");
                    }

                    sourceTeamA.Budget = decimal.Round(sourceTeamA.Budget - normalizedBalance.Value, 2, MidpointRounding.AwayFromZero);
                    sourceTeamB.Budget = decimal.Round(sourceTeamB.Budget + normalizedBalance.Value, 2, MidpointRounding.AwayFromZero);

                    ledgerEntries.Add(new BudgetLedger
                    {
                        BudgetLedgerId = Guid.NewGuid(),
                        TeamId = sourceTeamA.TeamId,
                        DataUtc = now,
                        Tipo = "DEBIT",
                        Origem = LedgerOriginTrade,
                        Valor = normalizedBalance.Value,
                        Descricao = description
                    });

                    ledgerEntries.Add(new BudgetLedger
                    {
                        BudgetLedgerId = Guid.NewGuid(),
                        TeamId = sourceTeamB.TeamId,
                        DataUtc = now,
                        Tipo = "CREDIT",
                        Origem = LedgerOriginTrade,
                        Valor = normalizedBalance.Value,
                        Descricao = description
                    });
                }
                else
                {
                    var absolute = Math.Abs(normalizedBalance.Value);
                    if (sourceTeamB.Budget < absolute)
                    {
                        throw new MarketSyncException("O time B não possui orçamento suficiente para o ajuste da troca.");
                    }

                    sourceTeamB.Budget = decimal.Round(sourceTeamB.Budget - absolute, 2, MidpointRounding.AwayFromZero);
                    sourceTeamA.Budget = decimal.Round(sourceTeamA.Budget + absolute, 2, MidpointRounding.AwayFromZero);

                    ledgerEntries.Add(new BudgetLedger
                    {
                        BudgetLedgerId = Guid.NewGuid(),
                        TeamId = sourceTeamB.TeamId,
                        DataUtc = now,
                        Tipo = "DEBIT",
                        Origem = LedgerOriginTrade,
                        Valor = absolute,
                        Descricao = description
                    });

                    ledgerEntries.Add(new BudgetLedger
                    {
                        BudgetLedgerId = Guid.NewGuid(),
                        TeamId = sourceTeamA.TeamId,
                        DataUtc = now,
                        Tipo = "CREDIT",
                        Origem = LedgerOriginTrade,
                        Valor = absolute,
                        Descricao = description
                    });
                }
            }

            playerA.CurrentTeamId = sourceTeamB.TeamId;
            playerB.CurrentTeamId = sourceTeamA.TeamId;

            await SyncRosterAsync(sourceTeamB.TeamId, playerA.PlayerId).ConfigureAwait(false);
            await SyncRosterAsync(sourceTeamA.TeamId, playerB.PlayerId).ConfigureAwait(false);

            var culture = CultureInfo.GetCultureInfo("pt-BR");
            var notes = normalizedBalance.HasValue && normalizedBalance.Value != 0m
                ? $"Troca com ajuste de R${Math.Abs(normalizedBalance.Value).ToString("N2", culture)}"
                : "Troca direta entre clubes";

            var historyEntries = new[]
            {
                new TransferHistory
                {
                    TransferId = Guid.NewGuid(),
                    Type = TransferType.TeamTrade,
                    PlayerId = playerA.PlayerId,
                    FromTeamId = sourceTeamA.TeamId,
                    ToTeamId = sourceTeamB.TeamId,
                    Amount = null,
                    Notes = notes,
                    PerformedBy = SystemActor,
                    PerformedAtUtc = now
                },
                new TransferHistory
                {
                    TransferId = Guid.NewGuid(),
                    Type = TransferType.TeamTrade,
                    PlayerId = playerB.PlayerId,
                    FromTeamId = sourceTeamB.TeamId,
                    ToTeamId = sourceTeamA.TeamId,
                    Amount = null,
                    Notes = notes,
                    PerformedBy = SystemActor,
                    PerformedAtUtc = now
                }
            };

            await _dbContext.TransferHistories.AddRangeAsync(historyEntries).ConfigureAwait(false);

            if (ledgerEntries.Count > 0)
            {
                await _dbContext.BudgetLedgers.AddRangeAsync(ledgerEntries).ConfigureAwait(false);
            }

            var logEntry = new AdminActionsLog
            {
                ActionId = Guid.NewGuid(),
                ActionType = AdminActionType.MarketTeamTrade,
                PerformedBy = SystemActor,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    playerIdA,
                    teamA,
                    playerIdB,
                    teamB,
                    balance = normalizedBalance
                }, JsonOptions),
                CreatedAtUtc = now
            };

            await _dbContext.AdminActionsLogs.AddAsync(logEntry).ConfigureAwait(false);

            await _dbContext.SaveChangesAsync().ConfigureAwait(false);

            if (ownsTransaction && transaction is not null)
            {
                await transaction.CommitAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            if (ownsTransaction && transaction is not null)
            {
                await transaction.RollbackAsync().ConfigureAwait(false);
            }

            throw;
        }
        finally
        {
            if (ownsTransaction && transaction is not null)
            {
                await transaction.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private async Task SyncRosterAsync(Guid teamId, int playerId)
    {
        var entries = await _dbContext.TeamRosters
            .Where(r => r.PlayerId == playerId)
            .ToListAsync()
            .ConfigureAwait(false);

        foreach (var entry in entries)
        {
            if (entry.TeamId != teamId)
            {
                _dbContext.TeamRosters.Remove(entry);
            }
        }

        var hasEntry = entries.Any(e => e.TeamId == teamId);
        if (!hasEntry)
        {
            await _dbContext.TeamRosters.AddAsync(new TeamRoster
            {
                PlayerId = playerId,
                TeamId = teamId
            }).ConfigureAwait(false);
        }
    }
}
