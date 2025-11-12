namespace Fc25Draft.Core.Entities;

public class TeamLineup
{
    public Guid LineupId { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = default!;
    public string FormationCode { get; set; } = default!;
    public string TacticCode { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? Observation { get; set; }
    public int? CaptainPlayerId { get; set; }
    public int? ShortFreeKickLeftPlayerId { get; set; }
    public int? ShortFreeKickRightPlayerId { get; set; }
    public int? LongFreeKickPlayerId { get; set; }
    public int? PenaltyKickPlayerId { get; set; }
    public int? LeftCornerPlayerId { get; set; }
    public int? RightCornerPlayerId { get; set; }

    public Team Team { get; set; } = default!;
    public ICollection<TeamLineupSlot> Slots { get; set; } = new List<TeamLineupSlot>();
}
