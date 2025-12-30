namespace Fc25Draft.Core.Entities
{
    public class DraftPick
    {
        public Guid DraftPickId { get; set; }
        public Guid DraftId { get; set; }
        public int OverallPick { get; set; }

        public int RoundNumber { get; set; }
        public int PickInRound { get; set; }
        public Guid? OwnerTeamId { get; set; }

        public int? PlayerId { get; set; }
        public DateTime? PickedAtUtc { get; set; }
        public DraftPickStatus Status { get; set; } = DraftPickStatus.Unassigned;
        public uint RowVersion { get; set; }

        public Draft Draft { get; set; } = null!;
        public DraftRound Round { get; set; } = null!;
        public Team? Team { get; set; }
        public Player? Player { get; set; }
    }
}
