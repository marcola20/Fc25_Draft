using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Competitions;

public sealed class CompetitionMatchEventsRequest
{
    [Required]
    public List<CompetitionMatchEventRequest> Events { get; set; } = new();
}
