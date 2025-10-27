using Fc25Draft.Core.Entities;
using Fc25Draft.Core.Interfaces;
using Fc25Draft.Core.Options;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Fc25Draft.Infra.Services;

public class PricingService : IPricingService
{
    private const int MinAge = 16;
    private const int MaxAge = 45;
    private const int MinOverall = 60;
    private const int MaxOverall = 99;

    private readonly DraftDbContext _dbContext;
    private readonly PricingOptions _options;
    private readonly IReadOnlyDictionary<string, decimal> _positionMultipliers;
    private readonly IReadOnlyList<PricingAgeMultiplier> _ageMultipliers;

    private static readonly IReadOnlyDictionary<short, string> PositionCodeById = new Dictionary<short, string>
    {
        [1] = "GK",
        [2] = "CB",
        [3] = "LB",
        [4] = "RB",
        [5] = "DM",
        [6] = "CM",
        [7] = "AM",
        [8] = "W",
        [9] = "W",
        [10] = "ST"
    };

    private static readonly IReadOnlyDictionary<string, string> PositionCodeByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Goleiro"] = "GK",
        ["Zagueiro"] = "CB",
        ["Lateral/Ala Esquerdo"] = "LB",
        ["Lateral/Ala Direito"] = "RB",
        ["Volante"] = "DM",
        ["Meia Central"] = "CM",
        ["Meia Atacante"] = "AM",
        ["Meia/Ponta Esquerda"] = "W",
        ["Meia/Ponta Direita"] = "W",
        ["Atacante"] = "ST"
    };

    public PricingService(DraftDbContext dbContext, IOptions<PricingOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        var configuredPositionMultipliers = _options.MultiplicadoresPosicao ?? new Dictionary<string, decimal>();
        _positionMultipliers = new Dictionary<string, decimal>(configuredPositionMultipliers, StringComparer.OrdinalIgnoreCase);
        var configuredAgeMultipliers = _options.MultiplicadoresIdade ?? new List<PricingAgeMultiplier>();
        _ageMultipliers = configuredAgeMultipliers.ToList();
    }

    public async Task<PricingResult> CalculateAsync(string? positionCode, short? positionId, int age, int overall, CancellationToken cancellationToken = default)
    {
        ValidateAge(age);
        ValidateOverall(overall);

        var code = await ResolvePositionCodeAsync(positionCode, positionId, cancellationToken).ConfigureAwait(false);
        var positionMultiplier = ResolvePositionMultiplier(code);
        var ageMultiplier = ResolveAgeMultiplier(age);

        var pOverall = _options.Alpha * (overall - 70) + _options.Beta;
        var basePrice = RoundToTenth(pOverall * positionMultiplier * ageMultiplier);
        var initialBid = RoundToTenth(basePrice * _options.PercentualLanceInicial);
        var buyNow = RoundToTenth(basePrice * _options.PercentualComprarAgora);

        return new PricingResult(basePrice, initialBid, buyNow);
    }

    public async Task<PricingResult> CalculateForPlayerAsync(int playerId, CancellationToken cancellationToken = default)
    {
        var player = await _dbContext.Players
            .AsNoTracking()
            .Include(p => p.Position)
            .FirstOrDefaultAsync(p => p.PlayerId == playerId, cancellationToken)
            .ConfigureAwait(false);

        if (player is null)
        {
            throw new KeyNotFoundException($"Jogador não encontrado: {playerId}.");
        }

        if (player.Age is null)
        {
            throw new InvalidOperationException($"Jogador {player.Name} não possui idade cadastrada.");
        }

        var positionCode = ResolvePositionCode(player);
        return await CalculateAsync(positionCode, player.PositionId, player.Age.Value, player.Overall, cancellationToken).ConfigureAwait(false);
    }

    public decimal RoundToTenth(decimal value)
    {
        return Math.Round(value * 10m, MidpointRounding.AwayFromZero) / 10m;
    }

    public decimal NextMinIncrement(decimal currentBid)
    {
        if (currentBid < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentBid));
        }

        var fivePercent = currentBid * 0.05m;
        var roundedUp = Math.Ceiling(fivePercent / 0.1m) * 0.1m;
        var increment = Math.Max(0.2m, roundedUp);
        return RoundToTenth(increment);
    }

    private void ValidateAge(int age)
    {
        if (age < MinAge || age > MaxAge)
        {
            throw new ArgumentException($"Idade fora do intervalo permitido ({MinAge}–{MaxAge}).");
        }
    }

    private void ValidateOverall(int overall)
    {
        if (overall < MinOverall || overall > MaxOverall)
        {
            throw new ArgumentException($"Overall fora do intervalo permitido ({MinOverall}–{MaxOverall}).");
        }
    }

    private async Task<string> ResolvePositionCodeAsync(string? positionCode, short? positionId, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(positionCode))
        {
            return positionCode.Trim().ToUpperInvariant();
        }

        if (positionId.HasValue)
        {
            var code = await ResolvePositionCodeAsync(positionId.Value, cancellationToken).ConfigureAwait(false);
            return code;
        }

        throw new ArgumentException("Código ou identificador de posição deve ser informado.");
    }

    private async Task<string> ResolvePositionCodeAsync(short positionId, CancellationToken cancellationToken)
    {
        if (PositionCodeById.TryGetValue(positionId, out var code))
        {
            return code;
        }

        var position = await _dbContext.Positions
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PositionId == positionId, cancellationToken)
            .ConfigureAwait(false);

        if (position is null)
        {
            throw new ArgumentException($"Posição não encontrada para o identificador informado: {positionId}.");
        }

        if (PositionCodeByName.TryGetValue(position.Name, out code))
        {
            return code;
        }

        throw new ArgumentException($"Posição desconhecida: {position.Name}.");
    }

    private string ResolvePositionCode(Player player)
    {
        if (PositionCodeById.TryGetValue(player.PositionId, out var code))
        {
            return code;
        }

        if (player.Position != null && PositionCodeByName.TryGetValue(player.Position.Name, out code))
        {
            return code;
        }

        throw new ArgumentException($"Posição desconhecida para o jogador: {player.PositionId}.");
    }

    private decimal ResolvePositionMultiplier(string positionCode)
    {
        if (_positionMultipliers.TryGetValue(positionCode, out var multiplier))
        {
            return multiplier;
        }

        throw new ArgumentException($"Posição desconhecida: {positionCode}");
    }

    private decimal ResolveAgeMultiplier(int age)
    {
        foreach (var multiplier in _ageMultipliers)
        {
            if (multiplier.Matches(age))
            {
                return multiplier.Multiplier;
            }
        }

        throw new ArgumentException($"Faixa etária não configurada para idade {age}.");
    }
}
