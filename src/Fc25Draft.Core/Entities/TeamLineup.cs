namespace Fc25Draft.Core.Entities;

public class TeamLineup
{
    public Guid LineupId { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = null!;
    public string Formation { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Team Team { get; set; } = null!;
    public ICollection<TeamLineupSlot> Slots { get; set; } = new List<TeamLineupSlot>();
}
