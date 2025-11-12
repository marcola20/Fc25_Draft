namespace Fc25Draft.Core.Entities;

public class TeamLineup
{
    public Guid LineupId { get; set; }
    public Guid TeamId { get; set; }
    public string FormationCode { get; set; } = default!;
    public string TacticCode { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Team Team { get; set; } = default!;
    public ICollection<TeamLineupSlot> Slots { get; set; } = new List<TeamLineupSlot>();
}
