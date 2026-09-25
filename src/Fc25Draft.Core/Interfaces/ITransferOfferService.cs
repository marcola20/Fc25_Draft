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

    /// <summary>Todas as propostas pendentes entre times, de qualquer time (uso administrativo).</summary>
    Task<IReadOnlyList<TransferOfferListItemDto>> GetAllPendingOffersAsync(CancellationToken ct);
    Task<TransferOfferListItemDto> CancelOfferAsync(Guid offerId, Guid teamId, CancellationToken ct);

    /// <summary>Todos os jogadores negociáveis (lista de transferência), de todos os times.</summary>
    Task<IReadOnlyList<ListaTransferenciaItemDto>> GetTransferListAsync(CancellationToken ct);

    /// <summary>Coloca (ou tira, com preço nulo) um jogador do próprio time na lista de transferência.</summary>
    Task SetAskingPriceAsync(Guid teamId, Guid playerGuid, decimal? askingPrice, CancellationToken ct);

    /// <summary>Compra direta de um jogador negociável pelo preço pedido: vira uma proposta de venda já aceita.</summary>
    Task<TransferOfferListItemDto> BuyListedPlayerAsync(Guid buyerTeamId, Guid playerGuid, decimal expectedPrice, CancellationToken ct);
}
