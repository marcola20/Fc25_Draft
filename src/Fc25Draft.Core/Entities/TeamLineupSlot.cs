namespace Fc25Draft.Core.Entities;

public class TeamLineupSlot
{
    public Guid SlotId { get; set; }
    public Guid LineupId { get; set; }
    public int Order { get; set; }
    public byte Role { get; set; }
    public int PrimaryPositionId { get; set; }
    public int? PlayerId { get; set; }

    public TeamLineup Lineup { get; set; } = default!;
    public Player? Player { get; set; }
}
