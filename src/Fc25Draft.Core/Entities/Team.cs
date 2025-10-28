namespace Fc25Draft.Core.Entities;

public class Team
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = null!;
    public string? OwnerName { get; set; }
    public string Token { get; set; } = null!;
    public decimal Budget { get; set; }
    public decimal BudgetBlocked { get; set; }

    public ICollection<TeamRoster> Roster { get; set; } = new List<TeamRoster>();
    public ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
    public ICollection<MarketBid> MarketBids { get; set; } = new List<MarketBid>();
}
