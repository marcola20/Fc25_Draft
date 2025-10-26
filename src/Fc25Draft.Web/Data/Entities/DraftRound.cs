namespace Fc25Draft.Web.Data.Entities
{
    public class DraftRound
    {
        public Guid DraftId { get; set; }
        public int RoundNumber { get; set; }

        public int? OverallMin { get; set; } 
        public int? OverallMax { get; set; }

        public Draft Draft { get; set; } = null!;
        public ICollection<DraftPick> Picks { get; set; } = new List<DraftPick>();
    }
}
