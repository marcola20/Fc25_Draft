using System;

namespace Fc25Draft.Web.Models.Market;

public class MarketItemVm
{
    public Guid ItemId { get; set; }

    public int PlayerId { get; set; }

    public string PlayerName { get; set; } = string.Empty;

    public int PositionId { get; set; }

    public int Overall { get; set; }

    public Guid? CurrentLeaderTeamId { get; set; }

    public string? CurrentLeaderTeamName { get; set; }

    public decimal? CurrentLeaderAmount { get; set; }

    public decimal BasePrice { get; set; }

    public decimal? BuyNowPrice { get; set; }

    public decimal MinIncrement { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public uint RowVersion { get; set; }
}
