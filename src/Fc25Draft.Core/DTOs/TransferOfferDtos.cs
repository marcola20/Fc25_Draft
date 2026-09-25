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
    Guid? ParentOfferId);

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
    DateTime UpdatedAtUtc);

public record TransferOfferPlayerDto(
    int PlayerId,
    Guid PlayerGuid,
    string Name,
    string Position,
    int Overall,
    int? Age);

/// <summary>Jogador colocado como negociável pelo dono, com o preço pedido.</summary>
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
    DateTime ListedAtUtc);

/// <summary>Preço pedido; nulo tira o jogador da lista de transferência.</summary>
public record SetAskingPriceDto(decimal? AskingPrice);

/// <summary>Compra direta pelo preço pedido. O preço vai junto para não pagar um valor que mudou no meio do caminho.</summary>
public record BuyListedPlayerDto(decimal ExpectedPrice);
