using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.DTOs;

public sealed record MarketItemListDto(
    Guid ItemId,
    Guid CycleId,
    int PlayerId,
    string PlayerName,
    string Position,
    int Overall,
    int Age,
    decimal BasePrice,
    decimal? CurrentBid,
    decimal? BuyNowPrice,
    decimal MinIncrement,
    decimal RequiredMinBid,
    DateTime ExpiresAtUtc,
    MarketItemStatus Status,
    string StatusText,
    Guid? CurrentLeaderTeamId,
    string? CurrentLeaderTeamName,
    uint RowVersion);

public enum MarketItemsSortField
{
    ExpiresAtUtc,
    CurrentBid
}

public sealed record MarketItemsQuery(
    Guid CycleId,
    string? Search,
    IReadOnlyList<short> PositionIds,
    int? OverallMin,
    int? OverallMax,
    IReadOnlyList<MarketItemStatus> Statuses,
    MarketItemsSortField SortBy,
    bool SortDescending,
    int Page,
    int PageSize);
