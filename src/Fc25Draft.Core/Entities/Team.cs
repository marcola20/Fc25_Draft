namespace Fc25Draft.Core.Entities
{
    public class Team
    {
        public Guid TeamId { get; set; }
        public string TeamName { get; set; } = null!;
        public string? OwnerName { get; set; }
        public Guid TeamToken { get; set; }  

        public ICollection<TeamRoster> Roster { get; set; } = new List<TeamRoster>();
        public ICollection<DraftPick> DraftPicks { get; set; } = new List<DraftPick>();
    }
}
