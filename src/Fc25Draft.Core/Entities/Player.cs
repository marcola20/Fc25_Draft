using System;

namespace Fc25Draft.Core.Entities;

public class Player
{
    public int PlayerId { get; set; }
    public Guid PlayerGuid { get; set; }
    public string Name { get; set; } = null!;
    public int? Age { get; set; }
    public int Overall { get; set; }
    public short PositionId { get; set; }
    public Guid? CurrentTeamId { get; set; }
    public Guid? QuickSellTeamId { get; set; }
    public int? QuickSellOldOverall { get; set; }
    public int? QuickSellNewOverall { get; set; }

    public Position Position { get; set; } = null!;
    public ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
    public ICollection<TeamRoster> TeamRosters { get; set; } = new List<TeamRoster>();
    public Team? CurrentTeam { get; set; }
    public ICollection<MarketItem> MarketItems { get; set; } = new List<MarketItem>();
}
