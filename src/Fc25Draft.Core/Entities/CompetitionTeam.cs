using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public sealed class CompetitionTeam
{
    public Guid CompetitionTeamId { get; set; }
    public Guid CompetitionId { get; set; }
    public Guid TeamId { get; set; }

    public bool IsActive { get; set; } = true;
    public decimal? InitialBudget { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Competition Competition { get; set; } = default!;
    public Team Team { get; set; } = default!;
    public ICollection<CompetitionMatch> HomeMatches { get; set; } = new List<CompetitionMatch>();
    public ICollection<CompetitionMatch> AwayMatches { get; set; } = new List<CompetitionMatch>();
    public ICollection<CompetitionMatchEvent> Events { get; set; } = new List<CompetitionMatchEvent>();
    public ICollection<CompetitionStanding> Standings { get; set; } = new List<CompetitionStanding>();
    public ICollection<CompetitionPlayerStat> PlayerStats { get; set; } = new List<CompetitionPlayerStat>();
    public ICollection<CompetitionTeamStat> TeamStats { get; set; } = new List<CompetitionTeamStat>();
}
