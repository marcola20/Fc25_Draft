namespace Fc25Draft.Core.Entities;

public class TeamLineupSlot
{
    public Guid LineupSlotId { get; set; }
    public Guid LineupId { get; set; }
    public string SlotCode { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public bool IsBench { get; set; }
    public int Order { get; set; }
    public int? PlayerId { get; set; }

    public TeamLineup Lineup { get; set; } = null!;
    public Player? Player { get; set; }
}
