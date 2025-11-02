using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.DTOs;

public class MarketQueryVm
{
    public string? Name { get; set; }

    public List<int> Positions { get; set; } = new();

    public int? OverallMin { get; set; }

    public int? OverallMax { get; set; }

    public string? Status { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Sort { get; set; }
}

public class MarketItemVm
{
    public Guid ItemId { get; set; }

    public Guid CycleId { get; set; }

    public int PlayerId { get; set; }

    public string PlayerName { get; set; } = string.Empty;

    public int PositionId { get; set; }

    public int Overall { get; set; }

    public decimal BasePrice { get; set; }

    public Guid? CurrentLeaderTeamId { get; set; }

    public string? CurrentLeaderTeamName { get; set; }

    public decimal? CurrentLeaderAmount { get; set; }

    public decimal? BuyNowPrice { get; set; }

    public decimal MinIncrement { get; set; }

    public decimal RequiredMinBid { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public bool IsActive { get; set; }

    public string Status { get; set; } = string.Empty;

    public string RowVersion { get; set; } = string.Empty;
}

public class BidRequest
{
    public Guid ItemId { get; set; }

    public decimal Amount { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}

public class BuyNowRequest
{
    public Guid ItemId { get; set; }

    public string RowVersion { get; set; } = string.Empty;
}
