using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Calendar;

public sealed class SeasonUpsertRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string? Name { get; set; }

    public bool IsActive { get; set; } = true;
}
