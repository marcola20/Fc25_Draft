namespace Fc25Draft.Core.Interfaces;

public interface IPricingService
{
    Task<PricingResult> CalculateAsync(string? positionCode, short? positionId, int age, int overall, CancellationToken cancellationToken = default);
    Task<PricingResult> CalculateForPlayerAsync(int playerId, CancellationToken cancellationToken = default);
    decimal RoundToTenth(decimal value);
    decimal NextMinIncrement(decimal currentBid);
}

public record PricingResult(decimal PrecoBase, decimal LanceInicial, decimal ComprarAgora);
