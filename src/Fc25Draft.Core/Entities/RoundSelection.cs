using System;
using System.Collections.Generic;

namespace Fc25Draft.Core.Entities;

public sealed class RoundSelection
{
    public Guid RoundSelectionId { get; set; }
    public Guid RoundId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Round Round { get; set; } = default!;
    public ICollection<RoundSelectionPlayer> Players { get; set; } = new List<RoundSelectionPlayer>();
}
