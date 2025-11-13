using System.ComponentModel.DataAnnotations;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Web.Models.Competitions;

public sealed class CompetitionMatchEventRequest
{
    public Guid? CompetitionMatchEventId { get; set; }

    [Required]
    public Guid? CompetitionTeamId { get; set; }

    public int? PlayerId { get; set; }

    public int? RelatedPlayerId { get; set; }

    [Required]
    public CompetitionMatchEventType? EventType { get; set; }

    [Range(0, 150)]
    public int? Minute { get; set; }

    [StringLength(400)]
    public string? Observations { get; set; }
}
