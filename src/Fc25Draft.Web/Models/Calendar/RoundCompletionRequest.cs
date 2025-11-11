using System;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Calendar;

public sealed class RoundCompletionRequest
{
    [Required]
    public bool? IsCompleted { get; set; }

    public DateTime? PlayedAtUtc { get; set; }
}
