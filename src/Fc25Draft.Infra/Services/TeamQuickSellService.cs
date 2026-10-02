using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Exceptions;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
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
            throw new QuickSellException("Time inválido.", StatusCodes.Status400BadRequest);

        if (playerId == Guid.Empty)
            throw new QuickSellException("Jogador inválido.", StatusCodes.Status400BadRequest);

        if (string.IsNullOrWhiteSpace(teamToken))
            throw new QuickSellException("Token do time é obrigatório.", StatusCodes.Status401Unauthorized);

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

                var tokenValido = await _dbContext.TokenComandaAsync(normalizedToken, teamId, ct).ConfigureAwait(false);
                if (!tokenValido)
                    throw new QuickSellException("Token do time inválido.", StatusCodes.Status403Forbidden);

                var player = await _dbContext.Players
                    .Include(p => p.TeamRosters)
                    .Include(p => p.Atributos)
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

                var emprestado = await _dbContext.Emprestimos
                    .AnyAsync(e => e.PlayerId == player.PlayerId && e.Status == EmprestimoStatus.Ativo, ct)
                    .ConfigureAwait(false);
                if (emprestado)
                    throw new QuickSellException("Jogador emprestado não pode ser vendido.", StatusCodes.Status409Conflict);

                var cfg = await _dbContext.TransferConfigs.AsNoTracking().FirstOrDefaultAsync(ct).ConfigureAwait(false)
                    ?? TransferConfig.Default();

                var rosterCount = await _dbContext.TeamRosters
                    .CountAsync(r => r.TeamId == teamId, ct)
                    .ConfigureAwait(false);

                var minRoster = cfg.MinRosterSizeFor(team);
                if (rosterCount <= minRoster)
                    throw new QuickSellException(rosterCount < minRoster
                        ? $"O elenco está abaixo do mínimo ({rosterCount} de {minRoster} jogadores): só dá para contratar até voltar ao mínimo."
                        : $"Você não pode realizar esta ação. O time ficaria com menos de {minRoster} jogadores.", StatusCodes.Status409Conflict);

                if (cfg.QuickSellBloqueado)
                    throw new QuickSellException("As vendas rápidas estão bloqueadas no momento.", StatusCodes.Status409Conflict);

                var maxQuickSell = cfg.MaxQuickSellFor(team);
                if (team.QuickSellCount >= maxQuickSell)
                    throw new QuickSellException($"Limite de vendas rápidas da janela atingido ({team.QuickSellCount}/{maxQuickSell}).", StatusCodes.Status429TooManyRequests);

                var now = _timeProvider.GetUtcNow().UtcDateTime;

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
                int oldOverall, newOverall;
                string? evolucao = null;
                if (player.Atributos is not null
                    && OverallPes.CalcularDoJogador(AtributosPes.ParaDto(player.Atributos)) is not null)
                {
                    // Com atributos do PES, a evolução é nos atributos; o overall sai da fórmula.
                    OverallPes.Recalcular(player);
                    oldOverall = player.Overall;
                    evolucao = EvoluirAtributos(player, QuickSellOverallCalculator.CalculateNewOverall(oldOverall), now);
                    newOverall = player.Overall;
                }
                else
                {
                    var hasPendingBump = player.QuickSellTeamId == teamId
                        && player.QuickSellOldOverall.HasValue
                        && player.QuickSellNewOverall.HasValue
                        && player.QuickSellNewOverall.Value == player.Overall;

                    oldOverall = hasPendingBump
                        ? player.QuickSellOldOverall!.Value
                        : player.Overall;

                    newOverall = hasPendingBump
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
                }

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
                team.QuickSellCount++;
                ExtratoCaixa.Lancar(_dbContext, team.TeamId, payout, ExtratoCaixa.VendaRapida,
                    $"Venda rápida de {player.Name}", _timeProvider.GetUtcNow().UtcDateTime);
                await PropostasPendentes.CancelarComJogadoresAsync(_dbContext, new[] { player.PlayerId }, null,
                    $"{player.Name} foi vendido na venda rápida.", _timeProvider.GetUtcNow().UtcDateTime, ct).ConfigureAwait(false);

                var historyNotes = BuildHistoryNotes(player.Name, oldOverall, newOverall, payout, evolucao);
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
                    now,
                    evolucao);
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

    /// <summary>
    /// Sobe os atributos até a fórmula do PES dar o overall-alvo, acerta o overall e deixa a mudança pendente
    /// para o Editor PES levar ao save do jogo. Devolve "Finalização +2, ..." ou null se nada mudou.
    /// </summary>
    private string? EvoluirAtributos(Player player, int alvo, DateTime now)
    {
        var dto = AtributosPes.ParaDto(player.Atributos!);
        var antes = OverallPes.Valores(dto);
        var overallAntes = player.Overall;
        var pos = dto.PosicaoPes!.Value;
        var evolucao = OverallPes.Evoluir(antes, pos, dto.EstiloDeJogo, dto.PeFracoUso, dto.PeFracoPrecisao, alvo);
        if (evolucao.Novos.SequenceEqual(antes))
            return null;

        for (var i = 0; i < antes.Length; i++)
            AtributosPes.Todos[i].Set(dto, evolucao.Novos[i]);
        AtributosPes.Aplicar(dto, player.Atributos!);
        OverallPes.Recalcular(player);

        _dbContext.EvolucoesPes.Add(new EvolucaoPes
        {
            PlayerId = player.PlayerId,
            Motivo = "Venda rápida",
            CriadaEmUtc = now,
            OverallAntes = overallAntes,
            OverallDepois = player.Overall,
            Mudancas = EvolucaoPes.EscreverMudancas(antes, evolucao.Novos),
        });

        if (!evolucao.Alcancou)
            _logger.LogWarning("Venda rápida de {PlayerId}: atributos no máximo, overall ficou em {Overall} (alvo {Alvo}).",
                player.PlayerId, player.Overall, alvo);
        return OverallPes.DescreverMudancas(antes, evolucao.Novos);
    }

    private static string BuildHistoryNotes(string playerName, int oldOver, int newOver, decimal payout, string? evolucao)
    {
        var culture = CultureInfo.GetCultureInfo("pt-BR");
        var formatted = payout.ToString("C", culture);
        var notas = $"Venda rápida de {playerName} por {formatted}.\n\r\n\r Over evoluído de {oldOver} para {newOver}!";
        return evolucao is null ? notas : $"{notas}\n\r Atributos: {evolucao}.";
    }
}
