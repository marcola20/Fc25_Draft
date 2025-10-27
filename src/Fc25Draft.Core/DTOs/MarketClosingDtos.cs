using System;

namespace Fc25Draft.Core.DTOs;

public record MarketClosePreviewDto(
    int OpenItems,
    IReadOnlyList<ItemPreviewDto> Items
);

public record ItemPreviewDto(
    Guid MarketItemId,
    int PlayerId,
    string PlayerName,
    string PositionName,
    int Age,
    int Overall,
    decimal? HighestBid,
    string? HighestBidTeam,
    bool HasEligibleWinner,
    string Decision
);

public record MarketCloseResultDto(
    int Processed,
    int Sold,
    int Expired,
    IReadOnlyList<ItemCloseResultDto> Items
);

public record ItemCloseResultDto(
    Guid MarketItemId,
    string StatusAfter,
    string? WinnerTeamName,
    decimal? WinnerBidValue,
    string OutcomeMessage
);
