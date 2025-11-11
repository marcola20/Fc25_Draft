using System;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Calendar;

public sealed class SeasonScheduleUpdateRequest
{
    [Required]
    [MinLength(1)]
    [ValidateComplexType]
    public List<SeasonScheduleUpdateItemRequest> Items { get; set; } = new();
}

public sealed class SeasonScheduleUpdateItemRequest
{
    [Range(1, 999)]
    public int Order { get; set; }

    [Required]
    public Guid RoundId { get; set; }
}
