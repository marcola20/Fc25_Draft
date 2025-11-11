namespace Fc25Draft.Core.Entities;

public sealed class RoundSelection
{
    public Guid RoundSelectionId { get; set; }
    public Guid RoundId { get; set; }
    public DateTime CreatedAt { get; set; }

    public Round Round { get; set; } = null!;
    public ICollection<RoundSelectionPlayer> Players { get; set; } = new List<RoundSelectionPlayer>();
}
