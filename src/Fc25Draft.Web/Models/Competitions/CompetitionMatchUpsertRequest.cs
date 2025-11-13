using System.ComponentModel.DataAnnotations;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Web.Models.Competitions;

public sealed class CompetitionMatchUpsertRequest
{
    public Guid? CompetitionMatchId { get; set; }

    [Required]
    public Guid? CompetitionId { get; set; }

    [Required]
    public Guid? RoundId { get; set; }

    [Required]
    public Guid? HomeCompetitionTeamId { get; set; }

    [Required]
    public Guid? AwayCompetitionTeamId { get; set; }

    public DateTime? MatchDateUtc { get; set; }

    [Range(0, 99)]
    public int? HomeGoals { get; set; }

    [Range(0, 99)]
    public int? AwayGoals { get; set; }

    [Required]
    public CompetitionMatchStatus? Status { get; set; } = CompetitionMatchStatus.Scheduled;

    [StringLength(150)]
    public string? Stadium { get; set; }

    [StringLength(400)]
    public string? Observations { get; set; }
}
