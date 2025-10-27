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
        var saldo = await _dbContext.TeamBudgets
            .AsNoTracking()
            .Where(tb => tb.TeamId == teamId)
            .Select(tb => (decimal?)tb.Saldo)
            .FirstOrDefaultAsync(ct);

        return saldo ?? 0m;
    }

    public async Task<decimal> GetBloqueadoEmLancesAsync(Guid teamId, CancellationToken ct)
    {
        var total = await _dbContext.TransferMarketItems
            .AsNoTracking()
            .Where(i => i.Status == "OPEN" && i.MaiorLanceTeamId == teamId && i.LanceAtual != null)
            .Select(i => (decimal?)i.LanceAtual)
            .SumAsync(ct);

        return decimal.Round(total ?? 0m, 2, MidpointRounding.AwayFromZero);
    }

    public async Task<decimal> GetSaldoDisponivelAsync(Guid teamId, CancellationToken ct)
    {
        var saldo = await GetSaldoAsync(teamId, ct);
        var bloqueado = await GetBloqueadoEmLancesAsync(teamId, ct);
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
            transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        try
        {
            var budget = await _dbContext.TeamBudgets
                .SingleOrDefaultAsync(tb => tb.TeamId == teamId, ct);

            if (budget is null)
            {
                var teamExists = await _dbContext.Teams.AnyAsync(t => t.TeamId == teamId, ct);
                if (!teamExists)
                {
                    throw new KeyNotFoundException($"Time {teamId} não encontrado.");
                }

                budget = new TeamBudget
                {
                    TeamId = teamId,
                    Saldo = 0m
                };
                await _dbContext.TeamBudgets.AddAsync(budget, ct);
            }

            budget.Saldo = credito
                ? budget.Saldo + normalizedValor
                : budget.Saldo - normalizedValor;

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

            await _dbContext.BudgetLedgers.AddAsync(ledgerEntry, ct);
            await _dbContext.SaveChangesAsync(ct);

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(ct);
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
            throw new ArgumentException("TeamId é obrigatório.", nameof(request.TeamId));
        }

        var teamExists = await _dbContext.Teams
            .AsNoTracking()
            .AnyAsync(t => t.TeamId == request.TeamId, ct);

        if (!teamExists)
        {
            throw new KeyNotFoundException($"Time {request.TeamId} não encontrado.");
        }

        var amount = CalculateMatchRewardAmount(request);
        var descricao = BuildMatchRewardDescription(request);
        var tipo = amount > 0 ? "CREDIT" : amount < 0 ? "DEBIT" : "NONE";
        var applied = amount != 0m;

        if (applied)
        {
            await RegistrarAjusteAsync(request.TeamId, Math.Abs(amount), "MATCH_REWARD", descricao, amount > 0, ct);
        }

        var saldoAtual = await GetSaldoAsync(request.TeamId, ct);
        return new MatchRewardResult(request.TeamId, amount, saldoAtual, applied, tipo, descricao);
    }

    private static string NormalizeResultado(string? resultado)
    {
        if (string.IsNullOrWhiteSpace(resultado))
        {
            throw new ArgumentException("Resultado é obrigatório.", nameof(resultado));
        }

        return resultado.Trim().ToUpperInvariant();
    }

    private static string BuildMatchRewardDescription(MatchRewardRequestDto request)
    {
        var resultado = NormalizeResultado(request.Resultado) switch
        {
            "VITORIA" => "Vitória",
            "EMPATE" => "Empate",
            "DERROTA" => "Derrota",
            _ => throw new ArgumentException("Resultado inválido.", nameof(request.Resultado))
        };

        var cleanSheetValue = request.CleanSheet ? 1 : 0;
        return $"{resultado}, {request.GolsFeitos} GM, {request.GolsSofridos} GS, CS={cleanSheetValue}";
    }
}
