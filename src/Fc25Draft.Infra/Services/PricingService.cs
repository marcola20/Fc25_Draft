using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

public class PricingService : IPricingService
{
    private readonly DraftDbContext _dbContext;
    private PricingConfig? _config;

    public PricingService(DraftDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    // Config de precificação (editável em /admin/configuracoes). Carregada uma vez por instância;
    // se ainda não houver linha no banco, usa os padrões (comportamento anterior).
    private PricingConfig Config => _config ??=
        _dbContext.PricingConfigs.AsNoTracking().FirstOrDefault() ?? PricingConfig.Default();

    public PricingResult Calculate(decimal positionWeight, int overall, int age)
    {
        if (overall <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(overall));
        }

        if (age <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(age));
        }

        if (positionWeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(positionWeight));
        }

        var cfg = Config;
        var overFactor = Math.Pow((double)cfg.OverallBase, overall - cfg.OverallPivot);
        var ageFactor = cfg.AgeFactor(age);

        var rawBasePrice = positionWeight * (decimal)overFactor * ageFactor * cfg.BaseScale;
        var normalizedBasePrice = Round(rawBasePrice, cfg.MinIncrementStep);

        var minIncrementBase = normalizedBasePrice * cfg.MinIncrementRate;
        var minIncrement = RoundUp(minIncrementBase, cfg.MinIncrementStep);

        var buyNowBase = normalizedBasePrice * cfg.BuyNowFactor;
        var buyNow = Round(buyNowBase, cfg.MinIncrementStep);

        return new PricingResult(normalizedBasePrice, minIncrement, buyNow);
    }

    public Task<PricingResult> CalculateForPositionAsync(string? positionCode, short? positionId, int age, int overall, CancellationToken ct)
    {
        if (!positionId.HasValue && string.IsNullOrWhiteSpace(positionCode))
        {
            throw new ArgumentException("Informe o identificador ou código da posição.");
        }

        var weight = positionId.HasValue
            ? MarketWeightResolver.GetByPositionId(positionId.Value)
            : MarketWeightResolver.GetByCode(positionCode!);

        return Task.FromResult(Calculate(weight, overall, age));
    }

    public async Task<PricingResult> CalculateForPlayerAsync(int playerId, CancellationToken ct)
    {
        var player = await _dbContext.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlayerId == playerId, ct)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Jogador {playerId} não encontrado.");

        if (!player.Age.HasValue)
        {
            throw new InvalidOperationException($"Jogador {player.Name} não possui idade cadastrada.");
        }

        var weight = MarketWeightResolver.GetByPositionId(player.PositionId);
        return Calculate(weight, player.Overall, player.Age.Value);
    }

    public decimal RoundUp(decimal value, decimal step)
    {
        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }

        if (value <= 0)
        {
            return step;
        }

        var quotient = decimal.Divide(value, step);
        var rounded = Math.Ceiling(quotient);
        return rounded * step;
    }

    public decimal Round(decimal value, decimal step)
    {
        if (step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step));
        }

        if (value <= 0)
        {
            return step;
        }

        var quotient = decimal.Divide(value, step);
        var rounded = Math.Round(quotient, MidpointRounding.AwayFromZero);
        if (rounded == 0)
        {
            rounded = 1;
        }

        return rounded * step;
    }
}
