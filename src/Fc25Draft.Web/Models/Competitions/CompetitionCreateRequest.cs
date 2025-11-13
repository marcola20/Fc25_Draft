using System.ComponentModel.DataAnnotations;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Web.Models.Competitions;

public sealed class CompetitionCreateRequest
{
    [Required]
    public Guid SeasonId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string? Name { get; set; }

    [Range(0, 999)]
    public int Order { get; set; }

    [Required]
    public CompetitionType? Type { get; set; } = CompetitionType.League;

    public bool IsActive { get; set; } = true;
}
