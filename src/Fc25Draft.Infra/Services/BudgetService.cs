using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Infra.Services;

public class BudgetService : IBudgetService
{
    private readonly DraftDbContext _dbContext;
    private readonly EconomiaOptions _economiaOptions;
    private readonly TimeProvider _timeProvider;

    public BudgetService(
        DraftDbContext dbContext,
        IOptions<EconomiaOptions> economiaOptions,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _economiaOptions = economiaOptions?.Value ?? throw new ArgumentNullException(nameof(economiaOptions));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<decimal> GetSaldoAsync(Guid teamId, CancellationToken ct)
    {
        var saldo = await _dbContext.Teams
            .AsNoTracking()
            .Where(t => t.TeamId == teamId)
            .Select(t => (decimal?)t.Budget)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return saldo ?? 0m;
    }

    public async Task<decimal> GetBloqueadoEmLancesAsync(Guid teamId, CancellationToken ct)
    {
        var total = await _dbContext.MarketItems
            .AsNoTracking()
            .Where(i => (i.Status == MarketItemStatus.Active || i.Status == MarketItemStatus.LeaderChanged) && i.CurrentLeaderTeamId == teamId && i.CurrentLeaderAmount != null)
            .Select(i => (decimal?)i.CurrentLeaderAmount)
            .SumAsync(ct)
            .ConfigureAwait(false);

        return decimal.Round(total ?? 0m, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<decimal> GetSaldoDisponivelAsync(Guid teamId, CancellationToken ct)
    {
        var saldo = await GetSaldoAsync(teamId, ct).ConfigureAwait(false);
        var bloqueado = await GetBloqueadoEmLancesAsync(teamId, ct).ConfigureAwait(false);
        return saldo - bloqueado;
    }

    public async Task RegistrarAjusteAsync(Guid teamId, decimal valor, string origem, string? descricao, bool credito, CancellationToken ct)
    {
        if (teamId == Guid.Empty)
        {
            throw new ArgumentException("TeamId inválido.", nameof(teamId));
        }

        if (valor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(valor), "Valor deve ser maior que zero.");
        }

        if (string.IsNullOrWhiteSpace(origem))
        {
            throw new ArgumentException("Origem obrigatória.", nameof(origem));
        }

        var normalizedValor = decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
        var normalizedOrigem = origem.Trim().ToUpperInvariant();
        var normalizedDescricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();

        var useTransaction = _dbContext.Database.IsRelational();
        IDbContextTransaction? transaction = null;

        if (useTransaction)
        {
            transaction = await _dbContext.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        }

        try
        {
            var team = await _dbContext.Teams
                .FirstOrDefaultAsync(t => t.TeamId == teamId, ct)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException($"Time {teamId} não encontrado.");

            team.Budget = credito
                ? team.Budget + normalizedValor
                : team.Budget - normalizedValor;

            if (team.Budget < 0m)
            {
                team.Budget = 0m;
            }

            var ledgerEntry = new BudgetLedger
            {
                BudgetLedgerId = Guid.NewGuid(),
                TeamId = teamId,
                DataUtc = _timeProvider.GetUtcNow().UtcDateTime,
                Tipo = credito ? "CREDIT" : "DEBIT",
                Origem = normalizedOrigem,
                Valor = normalizedValor,
                Descricao = normalizedDescricao
            };

            await _dbContext.BudgetLedgers.AddAsync(ledgerEntry, ct).ConfigureAwait(false);
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
    }

    public decimal CalculateMatchRewardAmount(MatchRewardRequestDto request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.GolsFeitos < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.GolsFeitos), "Gols feitos não pode ser negativo.");
        }

        if (request.GolsSofridos < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.GolsSofridos), "Gols sofridos não pode ser negativo.");
        }

        var resultado = NormalizeResultado(request.Resultado);

        decimal total = 0m;
        switch (resultado)
        {
            case "VITORIA":
                total += _economiaOptions.PremioVitoria;
                break;
            case "EMPATE":
                total += _economiaOptions.PremioEmpate;
                break;
            case "DERROTA":
                break;
            default:
                throw new ArgumentException("Resultado inválido.", nameof(request.Resultado));
        }

        if (request.GolsFeitos > 0)
        {
            total += request.GolsFeitos * _economiaOptions.PremioGolMarcado;
        }

        if (request.CleanSheet)
        {
            total += _economiaOptions.PremioCleanSheet;
        }

        if (request.GolsSofridos > 0)
        {
            total -= request.GolsSofridos * _economiaOptions.PenalidadeGolSofrido;
        }

        return decimal.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<MatchRewardResult> ApplyMatchRewardAsync(MatchRewardRequestDto request, CancellationToken ct)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request.TeamId == Guid.Empty)
        {
            throw new ArgumentException("TeamId inválido.", nameof(request.TeamId));
        }

        var total = CalculateMatchRewardAmount(request);

        var team = await _dbContext.Teams
            .FirstOrDefaultAsync(tb => tb.TeamId == request.TeamId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Time {request.TeamId} não encontrado.");

        team.Budget += total;

        var ledgerEntry = new BudgetLedger
        {
            BudgetLedgerId = Guid.NewGuid(),
            TeamId = request.TeamId,
            DataUtc = _timeProvider.GetUtcNow().UtcDateTime,
            Tipo = "CREDIT",
            Origem = "MATCH_REWARD",
            Valor = total,
            Descricao = request.Descricao
        };

        await _dbContext.BudgetLedgers.AddAsync(ledgerEntry, ct).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return new MatchRewardResult(request.TeamId, total);
    }

    private static string NormalizeResultado(string? resultado)
    {
        if (string.IsNullOrWhiteSpace(resultado))
        {
            throw new ArgumentException("Resultado obrigatório.", nameof(resultado));
        }

        return resultado.Trim().ToUpperInvariant();
    }
}
