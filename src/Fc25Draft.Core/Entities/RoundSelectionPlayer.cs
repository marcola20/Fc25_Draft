using System;

namespace Fc25Draft.Core.Entities;

public sealed class RoundSelectionPlayer
{
    public Guid RoundSelectionId { get; set; }
    public int PlayerId { get; set; }
    public DateTime AddedAtUtc { get; set; }

    public RoundSelection RoundSelection { get; set; } = default!;
    public Player Player { get; set; } = default!;
}
