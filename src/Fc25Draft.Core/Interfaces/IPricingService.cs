using System.Threading;
using System.Threading.Tasks;
using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IPricingService
{
    PricingResult Calculate(decimal positionWeight, int overall, int age);
    Task<PricingResult> CalculateForPositionAsync(string? positionCode, short? positionId, int age, int overall, CancellationToken ct);
    Task<PricingResult> CalculateForPlayerAsync(int playerId, CancellationToken ct);
    decimal RoundUp(decimal value, decimal step);
    decimal Round(decimal value, decimal step);
}

public record PricingResult(decimal BasePrice, decimal MinIncrement, decimal BuyNowPrice);
