using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketItemGenerationService
{
    Task<MarketItemGenerationPreview> PreviewAsync(Guid cycleId, MarketItemGenerationOptions options, CancellationToken ct);
    Task<MarketItemGenerationResult> GenerateAsync(Guid cycleId, MarketItemGenerationOptions options, CancellationToken ct);
    Task<int> DeleteDraftsAsync(Guid cycleId, CancellationToken ct);
}
