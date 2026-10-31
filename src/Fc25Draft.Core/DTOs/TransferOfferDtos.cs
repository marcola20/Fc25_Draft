using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

public record CreateTransferOfferDto(
    Guid FromTeamId,
    Guid ToTeamId,
    OfferType Type,
    Guid[] TargetPlayerIds,
    Guid[]? OfferedPlayerIds,
    decimal Money,
    Guid? MoneyPayerTeamId,
    decimal SellOnPercentage,
    string? Clauses,
    string? Notes,
    Guid? ParentOfferId,
    decimal? BuyOptionPrice = null,
    PickRefDto[]? TargetPicks = null,
    PickRefDto[]? OfferedPicks = null,
    decimal? BuybackPrice = null);

public record RespondToOfferDto(
    OfferStatus Response);

public record TransferOfferListItemDto(
    Guid OfferId,
    Guid FromTeamId,
    string FromTeamName,
    Guid ToTeamId,
    string ToTeamName,
    string Type,
    string Status,
    decimal Money,
    Guid? MoneyPayerTeamId,
    string? MoneyPayerTeamName,
    decimal SellOnPercentage,
    string? Clauses,
    string? Notes,
    Guid? ParentOfferId,
    IReadOnlyList<TransferOfferPlayerDto> TargetPlayers,
    IReadOnlyList<TransferOfferPlayerDto> OfferedPlayers,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    decimal? BuyOptionPrice = null,
    IReadOnlyList<PickDoDraftDto>? TargetPicks = null,
    IReadOnlyList<PickDoDraftDto>? OfferedPicks = null,
    decimal? BuybackPrice = null);

/// <summary>Cláusula de recompra: quem vendeu pode comprar de volta pelo preço combinado até o fim da temporada.</summary>
/// <param name="Vale">Ainda dá para recomprar (mesma temporada, jogador no clube que comprou, não exercida).</param>
/// <param name="Situacao">Por que não vale mais (exercida, jogador saiu, temporada acabou); nulo se vale.</param>
public record DireitoDeRecompraDto(
    Guid DireitoId,
    int PlayerId,
    Guid PlayerGuid,
    string PlayerName,
    Guid VendedorId,
    string VendedorNome,
    Guid CompradorId,
    string CompradorNome,
    decimal Preco,
    bool Vale,
    string? Situacao);

public record TransferOfferPlayerDto(
    int PlayerId,
    Guid PlayerGuid,
    string Name,
    string Position,
    int Overall,
    int? Age);

/// <summary>
/// Jogador colocado como negociável pelo dono, com o preço pedido. <see cref="PediuParaSair"/>: foi sozinho para a
/// lista pelo preço da venda rápida (<see cref="PromessaQuebrada"/>: pela metade dela).
/// </summary>
public record ListaTransferenciaItemDto(
    int PlayerId,
    Guid PlayerGuid,
    string Name,
    string Position,
    short PositionId,
    int Overall,
    int? Age,
    Guid TeamId,
    string TeamName,
    decimal AskingPrice,
    DateTime ListedAtUtc,
    bool PediuParaSair = false,
    bool PromessaQuebrada = false);

/// <summary>Preço pedido; nulo tira o jogador da lista de transferência.</summary>
public record SetAskingPriceDto(decimal? AskingPrice);

/// <summary>Compra direta pelo preço pedido. O preço vai junto para não pagar um valor que mudou no meio do caminho.</summary>
public record BuyListedPlayerDto(decimal ExpectedPrice);
