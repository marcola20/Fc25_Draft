using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Competitions;

public sealed class CompetitionRoundGenerationRequest
{
    public bool IncludeReturnLeg { get; set; }

    public DateTime? FirstRoundDateUtc { get; set; }

    [Range(1, 60)]
    public int? DaysBetweenRounds { get; set; }
}
