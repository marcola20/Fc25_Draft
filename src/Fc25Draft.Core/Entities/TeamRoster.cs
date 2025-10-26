namespace Fc25Draft.Core.Entities
{
    public class TeamRoster
    {
        public Guid TeamId { get; set; }
        public int PlayerId { get; set; }

        public Team Team { get; set; } = null!;
        public Player Player { get; set; } = null!;
    }
}
