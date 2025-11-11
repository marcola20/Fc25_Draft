using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Calendar;

public sealed class CompetitionUpsertRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string? Name { get; set; }

    [Range(1, 999)]
    public int Order { get; set; } = 1;

    public bool IsActive { get; set; } = true;
}
