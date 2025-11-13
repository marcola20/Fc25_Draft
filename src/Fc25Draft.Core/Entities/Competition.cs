using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public sealed class Competition
{
    public Guid CompetitionId { get; set; }
    public Guid SeasonId { get; set; }

    public string Name { get; set; } = default!;
    public int Order { get; set; }
    public CompetitionType Type { get; set; } = CompetitionType.League;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Season Season { get; set; } = default!;
    public ICollection<Round> Rounds { get; set; } = new List<Round>();
    public ICollection<CompetitionTeam> Teams { get; set; } = new List<CompetitionTeam>();
    public ICollection<CompetitionMatch> Matches { get; set; } = new List<CompetitionMatch>();
    public ICollection<CompetitionStanding> Standings { get; set; } = new List<CompetitionStanding>();
    public ICollection<CompetitionLog> Logs { get; set; } = new List<CompetitionLog>();
}
