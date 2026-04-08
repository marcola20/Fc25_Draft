namespace Fc25Draft.Core.Entities;

public class TeamLineup
{
    public Guid LineupId { get; set; }
    public Guid TeamId { get; set; }
    public string Name { get; set; } = null!;
    public string Formation { get; set; } = null!;
    public int AutoSubstitution { get; set; } = 1;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public int? CaptainPlayerId { get; set; }
    public int? ShortFreeKick1PlayerId { get; set; }
    public int? ShortFreeKick2PlayerId { get; set; }
    public int? LongFreeKickPlayerId { get; set; }
    public int? PenaltiesPlayerId { get; set; }
    public int? CornerLeftPlayerId { get; set; }
    public int? CornerRightPlayerId { get; set; }
    public int? AttackingPlayer1Id { get; set; }
    public int? AttackingPlayer2Id { get; set; }
    public int? AttackingPlayer3Id { get; set; }

    public Team Team { get; set; } = null!;
    public ICollection<TeamLineupSlot> Slots { get; set; } = new List<TeamLineupSlot>();
    public Player? CaptainPlayer { get; set; }
    public Player? ShortFreeKick1Player { get; set; }
    public Player? ShortFreeKick2Player { get; set; }
    public Player? LongFreeKickPlayer { get; set; }
    public Player? PenaltiesPlayer { get; set; }
    public Player? CornerLeftPlayer { get; set; }
    public Player? CornerRightPlayer { get; set; }
    public Player? AttackingPlayer1 { get; set; }
    public Player? AttackingPlayer2 { get; set; }
    public Player? AttackingPlayer3 { get; set; }
    public TeamLineupOffensiveInstructions? OffensiveInstructions { get; set; }
    public TeamLineupDefensiveInstructions? DefensiveInstructions { get; set; }
}
