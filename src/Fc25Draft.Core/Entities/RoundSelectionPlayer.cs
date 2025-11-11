namespace Fc25Draft.Core.Entities;

public sealed class RoundSelectionPlayer
{
    public Guid RoundSelectionId { get; set; }
    public Guid PlayerGuid { get; set; }
    public DateTime AddedAt { get; set; }

    public RoundSelection RoundSelection { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
