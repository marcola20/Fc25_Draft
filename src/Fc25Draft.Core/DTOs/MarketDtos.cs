namespace Fc25Draft.Core.DTOs;

public record MarketCycleDto(Guid CycleId, DateTime CreatedAtUtc, DateTime NextCycleAtUtc);

public record MarketItemDto(
    Guid ItemId,
    Guid CycleId,
    int PlayerId,
    string PlayerName,
    string Position,
    int Ovr,
    int Age,
    decimal BasePrice,
    decimal? BuyNowPrice,
    decimal MinIncrement,
    DateTime ExpiresAtUtc,
    string Status,
    decimal? CurrentLeaderAmount,
    string? CurrentLeaderTeamName,
    Guid? CurrentLeaderTeamId,
    uint RowVersion);

public record BidResultDto(bool Ok, string Message, decimal? LeaderAmount);

public record BuyNowResultDto(bool Ok, string Message);
