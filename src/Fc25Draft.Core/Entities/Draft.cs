namespace Fc25Draft.Core.Entities
{
    public class Draft
    {
        public Guid DraftId { get; set; }
        public string Name { get; set; } = null!;
        public int TotalTeams { get; set; }
        public int TotalRounds { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public ICollection<DraftRound> Rounds { get; set; } = new List<DraftRound>();
        public ICollection<DraftPick> Picks { get; set; } = new List<DraftPick>();
    }
}
