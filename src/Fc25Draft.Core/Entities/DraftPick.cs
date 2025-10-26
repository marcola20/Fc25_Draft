namespace Fc25Draft.Core.Entities
{
    public class DraftPick
    {
        public Guid DraftId { get; set; }
        public int OverallPick { get; set; }

        public int RoundNumber { get; set; }
        public int PickInRound { get; set; }
        public Guid TeamId { get; set; }

        public int? PlayerId { get; set; }
        public DateTime? PickedAtUtc { get; set; }

        public Draft Draft { get; set; } = null!;
        public DraftRound Round { get; set; } = null!;
        public Team Team { get; set; } = null!;
        public Player? Player { get; set; }
    }
}
