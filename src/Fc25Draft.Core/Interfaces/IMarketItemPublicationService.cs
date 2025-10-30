using Fc25Draft.Core.DTOs;

namespace Fc25Draft.Core.Interfaces;

public interface IMarketItemPublicationService
{
    Task<IReadOnlyList<MarketItemPublicationDto>> ListAsync(CancellationToken ct);
    Task<MarketItemPublicationDto?> GetAsync(Guid itemId, CancellationToken ct);
    Task<MarketItemPublicationDto> CreateDraftAsync(MarketItemDraftCreateRequest request, CancellationToken ct);
    Task<MarketItemPublicationDto> UpdateDraftAsync(Guid itemId, MarketItemDraftUpdateRequest request, uint expectedRowVersion, CancellationToken ct);
    Task<MarketItemPublicationDto> PublishAsync(Guid itemId, uint expectedRowVersion, CancellationToken ct);
    Task<MarketItemPublicationDto> SoftDeleteAsync(Guid itemId, uint expectedRowVersion, CancellationToken ct);
}
