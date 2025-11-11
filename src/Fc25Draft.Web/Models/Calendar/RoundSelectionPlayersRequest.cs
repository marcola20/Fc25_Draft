using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Fc25Draft.Web.Models.Calendar;

public sealed class RoundSelectionPlayersRequest
{
    [Required]
    public List<Guid> PlayerIds { get; set; } = new();
}
