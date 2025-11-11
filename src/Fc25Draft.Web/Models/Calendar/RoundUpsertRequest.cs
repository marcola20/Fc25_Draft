using System;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Calendar;

public sealed class RoundUpsertRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string? Name { get; set; }

    public bool IsCompleted { get; set; }

    public DateTime? PlayedAtUtc { get; set; }

    [StringLength(400)]
    public string? Notes { get; set; }
}
