namespace Fc25Draft.Core.DTOs;

public record MarketItemDraftCreateRequest(
    Guid CycleId,
    int PlayerId,
    decimal BasePrice,
    decimal? BuyNowPrice,
    decimal MinIncrement,
    DateTime ExpiresAtUtc);

public record MarketItemDraftUpdateRequest(
    decimal BasePrice,
    decimal? BuyNowPrice,
    decimal MinIncrement,
    DateTime ExpiresAtUtc);

public record MarketItemPublicationDto(
    Guid ItemId,
    Guid CycleId,
    int PlayerId,
    string PlayerName,
    string Position,
    int Overall,
    int? Age,
    decimal BasePrice,
    decimal? BuyNowPrice,
    decimal MinIncrement,
    DateTime ExpiresAtUtc,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc,
    DateTime LastUpdateUtc,
    uint RowVersion);
