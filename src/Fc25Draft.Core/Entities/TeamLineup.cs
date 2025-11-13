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

    public int? CaptainPlayerId { get; set; }
    public int? ShortFreeKickLeftPlayerId { get; set; }
    public int? ShortFreeKickRightPlayerId { get; set; }
    public int? LongFreeKickPlayerId { get; set; }
    public int? PenaltiesPlayerId { get; set; }
    public int? CornerLeftPlayerId { get; set; }
    public int? CornerRightPlayerId { get; set; }

    public Team Team { get; set; } = null!;
    public ICollection<TeamLineupSlot> Slots { get; set; } = new List<TeamLineupSlot>();
    public Player? CaptainPlayer { get; set; }
    public Player? ShortFreeKickLeftPlayer { get; set; }
    public Player? ShortFreeKickRightPlayer { get; set; }
    public Player? LongFreeKickPlayer { get; set; }
    public Player? PenaltiesPlayer { get; set; }
    public Player? CornerLeftPlayer { get; set; }
    public Player? CornerRightPlayer { get; set; }
}
