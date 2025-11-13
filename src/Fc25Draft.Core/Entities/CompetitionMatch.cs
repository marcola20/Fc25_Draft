using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public sealed class CompetitionMatch
{
    public Guid CompetitionMatchId { get; set; }
    public Guid CompetitionId { get; set; }
    public Guid RoundId { get; set; }
    public Guid HomeCompetitionTeamId { get; set; }
    public Guid AwayCompetitionTeamId { get; set; }

    public DateTime? MatchDateUtc { get; set; }
    public int? HomeGoals { get; set; }
    public int? AwayGoals { get; set; }
    public CompetitionMatchStatus Status { get; set; } = CompetitionMatchStatus.Scheduled;
    public string? Stadium { get; set; }
    public string? Observations { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Competition Competition { get; set; } = default!;
    public Round Round { get; set; } = default!;
    public CompetitionTeam HomeTeam { get; set; } = default!;
    public CompetitionTeam AwayTeam { get; set; } = default!;
    public ICollection<CompetitionMatchEvent> Events { get; set; } = new List<CompetitionMatchEvent>();
}
