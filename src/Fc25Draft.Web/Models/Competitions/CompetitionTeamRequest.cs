using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Competitions;

public sealed class CompetitionTeamRequest
{
    [Required]
    public Guid? TeamId { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal? InitialBudget { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
