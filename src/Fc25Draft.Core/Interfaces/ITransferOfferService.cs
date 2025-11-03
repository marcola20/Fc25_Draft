using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Interfaces;

public interface ITransferOfferService
{
    Task<TransferOffer> CreateOfferAsync(
        Guid fromTeamId,
        Guid toTeamId,
        int playerId,
        decimal? offeredFee,
        IEnumerable<int> swapPlayerIds,
        string? message,
        DateTime? expiresAtUtc,
        CancellationToken ct);

    Task<TransferOffer> AcceptOfferAsync(Guid offerId, uint expectedRowVersion, CancellationToken ct);
    Task<TransferOffer> RejectOfferAsync(Guid offerId, string? responseMessage, uint expectedRowVersion, CancellationToken ct);
    Task<TransferOffer> CancelOfferAsync(Guid offerId, Guid requestingTeamId, uint expectedRowVersion, CancellationToken ct);
    Task<TransferOffer> ExpireOfferAsync(Guid offerId, CancellationToken ct);
}
