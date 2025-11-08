using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Infra.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Fc25Draft.Infra.Services;

public class TeamQuickSellService : ITeamQuickSellService
{
    private readonly DraftDbContext _dbContext;
    private readonly IPricingService _pricingService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TeamQuickSellService> _logger;

    public TeamQuickSellService(
        DraftDbContext dbContext,
        IPricingService pricingService,
        ILogger<TeamQuickSellService> logger,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _pricingService = pricingService ?? throw new ArgumentNullException(nameof(pricingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<QuickSellResultDto> QuickSellAsync(Guid teamId, Guid playerId, string teamToken, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new QuickSellException("Time inválido.", StatusCodes.Status400BadRequest);
        }

        if (playerId == Guid.Empty)
        {
            throw new QuickSellException("Jogador inválido.", StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(teamToken))
        {
            throw new QuickSellException("Token do time é obrigatório.", StatusCodes.Status401Unauthorized);
        }

        var normalizedToken = teamToken.Trim();
        QuickSellResultDto? result = null;

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

            try
            {
                var team = await _dbContext.Teams
                    .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
                    .ConfigureAwait(false)
                    ?? throw new QuickSellException("Time não encontrado.", StatusCodes.Status404NotFound);

                if (!string.Equals(team.Token, normalizedToken, StringComparison.OrdinalIgnoreCase))
                    throw new QuickSellException("Token do time inválido.", StatusCodes.Status403Forbidden);

                var player = await _dbContext.Players
                    .Include(p => p.TeamRosters)
                    .FirstOrDefaultAsync(p => p.PlayerGuid == playerId, ct)
                    .ConfigureAwait(false)
                    ?? throw new QuickSellException("Jogador não encontrado.", StatusCodes.Status404NotFound);

                var teamRoster = player.TeamRosters.FirstOrDefault(r => r.TeamId == teamId);
                if (teamRoster != null)
                    player.CurrentTeamId = teamRoster.TeamId;
                else
                    throw new QuickSellException("Jogador não pertence ao time informado.", StatusCodes.Status404NotFound);

                if (player.TeamRosters.All(r => r.TeamId != teamId))
                    throw new QuickSellException("Jogador não pertence ao time informado.", StatusCodes.Status404NotFound);

                var rosterCount = await _dbContext.TeamRosters
                    .CountAsync(r => r.TeamId == teamId, ct)
                    .ConfigureAwait(false);

                if (rosterCount <= 18)
                    throw new QuickSellException("Você não pode realizar esta ação. O time ficaria com menos de 18 jogadores.", StatusCodes.Status409Conflict);

                PricingResult pricing;
                try
                {
                    pricing = await _pricingService.CalculateForPlayerAsync(player.PlayerId, ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or ArgumentException)
                {
                    _logger.LogError(ex, "Erro ao calcular o preço do jogador.");
                    throw new QuickSellException("Não foi possível calcular o preço base do jogador.", StatusCodes.Status400BadRequest);
                }

                var basePrice = pricing.BasePrice;
                if (basePrice <= 0m)
                    throw new QuickSellException("Preço base inválido para o jogador.", StatusCodes.Status400BadRequest);

                var payout = decimal.Round(basePrice * 0.8m, 2, MidpointRounding.AwayFromZero);
                var hasPendingBump = player.QuickSellTeamId == teamId
                    && player.QuickSellOldOverall.HasValue
                    && player.QuickSellNewOverall.HasValue
                    && player.QuickSellNewOverall.Value == player.Overall;

                var oldOverall = hasPendingBump
                    ? player.QuickSellOldOverall!.Value
                    : player.Overall;

                var newOverall = hasPendingBump
                    ? player.QuickSellNewOverall!.Value
                    : QuickSellOverallCalculator.CalculateNewOverall(oldOverall);

                if (!hasPendingBump)
                {
                    player.Overall = newOverall;
                    player.QuickSellTeamId = teamId;
                    player.QuickSellOldOverall = oldOverall;
                    player.QuickSellNewOverall = newOverall;

                    try
                    {
                        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                    catch (DbUpdateException dbEx)
                    {
                        _logger.LogError(dbEx, "Erro ao persistir o novo overall do jogador {PlayerId} na venda rápida.", player.PlayerId);
                        throw new QuickSellException("Não foi possível atualizar o overall do jogador.", StatusCodes.Status500InternalServerError);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro inesperado ao atualizar o overall do jogador {PlayerId} na venda rápida.", player.PlayerId);
                        throw new QuickSellException("Erro inesperado ao atualizar o overall do jogador.", StatusCodes.Status500InternalServerError);
                    }
                }

                var now = _timeProvider.GetUtcNow().UtcDateTime;

                player.CurrentTeamId = null;
                var rosterEntry = player.TeamRosters.FirstOrDefault(r => r.TeamId == teamId);
                if (rosterEntry is not null)
                {
                    _dbContext.TeamRosters.Remove(rosterEntry);
                }
                else
                {
                    var trackedEntry = await _dbContext.TeamRosters
                        .FirstOrDefaultAsync(r => r.TeamId == teamId && r.PlayerId == player.PlayerId, ct)
                        .ConfigureAwait(false);
                    if (trackedEntry is not null)
                        _dbContext.TeamRosters.Remove(trackedEntry);
                }

                team.Budget = decimal.Round(team.Budget + payout, 2, MidpointRounding.AwayFromZero);

                var historyNotes = BuildHistoryNotes(player.Name, payout);
                var historyEntry = new TransferHistory
                {
                    TransferId = Guid.NewGuid(),
                    Type = TransferType.QuickSell,
                    PlayerId = player.PlayerId,
                    FromTeamId = teamId,
                    ToTeamId = null,
                    Amount = payout,
                    Notes = historyNotes,
                    PerformedBy = team.TeamName,
                    PerformedAtUtc = now,
                    OldOverall = oldOverall,
                    NewOverall = newOverall
                };

                await _dbContext.TransferHistories.AddAsync(historyEntry, ct).ConfigureAwait(false);
                player.QuickSellTeamId = null;
                player.QuickSellOldOverall = null;
                player.QuickSellNewOverall = null;

                await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
                await transaction.CommitAsync(ct).ConfigureAwait(false);

                result = new QuickSellResultDto(
                    team.TeamId,
                    player.PlayerGuid,
                    oldOverall,
                    newOverall,
                    basePrice,
                    payout,
                    team.Budget,
                    now);
            }
            catch (QuickSellException)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
            catch (DbUpdateException dbEx)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogError(dbEx, "Erro ao atualizar o banco de dados.");
                throw new InvalidOperationException("Erro ao processar a venda rápida no banco de dados.", dbEx);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct).ConfigureAwait(false);
                _logger.LogError(ex, "Erro inesperado na transação.");
                throw new InvalidOperationException("Erro inesperado.", ex);
            }
        });

        return result!;
    }

    private static string BuildHistoryNotes(string playerName, decimal payout)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var formatted = payout.ToString("C", culture);
        return $"Venda rápida de {playerName} por {formatted}";
    }
}
