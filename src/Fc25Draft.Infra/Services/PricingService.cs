using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Options;
using Fc25Draft.Core.Utilities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Infra.Services;

public class PricingService : IPricingService
{
    private readonly DraftDbContext _dbContext;
    private readonly MarketOptions _marketOptions;

    public PricingService(DraftDbContext dbContext, IOptions<MarketOptions> options)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _marketOptions = options.Value ?? throw new ArgumentNullException(nameof(options));
    }

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

        var overFactor = Math.Pow(1.08d, overall - 75);
        var ageFactor = GetAgeFactor(age);

        var rawBasePrice = positionWeight * (decimal)overFactor * ageFactor * _marketOptions.BaseScale;
        var normalizedBasePrice = Round(rawBasePrice, _marketOptions.MinIncrementStep);

        var minIncrementBase = normalizedBasePrice * _marketOptions.MinIncrementRate;
        var minIncrement = RoundUp(minIncrementBase, _marketOptions.MinIncrementStep);

        var buyNowBase = normalizedBasePrice * _marketOptions.BuyNowFactor;
        var buyNow = Round(buyNowBase, _marketOptions.MinIncrementStep);

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

    private static decimal GetAgeFactor(int age)
    {
        if (age <= 22)
        {
            return 1.18m;
        }

        if (age <= 24)
        {
            return 1.15m;
        }

        if (age <= 26)
        {
            return 1.1m;
        }

        if (age <= 28)
        {
            return 1m;
        }

        if (age <= 30)
        {
            return 0.98m;
        }

        if (age <= 32)
        {
            return 0.95m;
        }

        if (age <= 34)
        {
            return 0.90m;
        }

        return 0.85m;
    }
}
