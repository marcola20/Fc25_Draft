using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Competitions;

public sealed class CompetitionToggleRequest
{
    [Required]
    public bool? IsActive { get; set; }
}
