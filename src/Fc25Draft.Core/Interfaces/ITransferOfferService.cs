using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

public interface ITransferOfferService
{
    Task<TransferOfferListItemDto> CreateOfferAsync(CreateTransferOfferDto dto, CancellationToken ct);
    Task<TransferOfferListItemDto> RespondToOfferAsync(Guid offerId, Guid teamId, OfferStatus response, CancellationToken ct);
    Task<IReadOnlyList<TransferOfferListItemDto>> GetReceivedOffersAsync(Guid teamId, CancellationToken ct);
    Task<IReadOnlyList<TransferOfferListItemDto>> GetSentOffersAsync(Guid teamId, CancellationToken ct);
    Task<IReadOnlyList<TransferOfferListItemDto>> GetFinishedOffersAsync(Guid teamId, CancellationToken ct);
    Task<TransferOfferListItemDto?> GetByIdAsync(Guid offerId, CancellationToken ct);
    Task<TransferOfferListItemDto> CancelOfferAsync(Guid offerId, Guid teamId, CancellationToken ct);
}
