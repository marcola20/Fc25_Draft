namespace Fc25Draft.Core.Entities;

public sealed class Round
{
    public Guid RoundId { get; set; }
    public Guid CompetitionId { get; set; }

    public string Name { get; set; } = default!;
    public bool IsCompleted { get; set; }
    public DateTime? PlayedAtUtc { get; set; }
    public string? Notes { get; set; }

    public Competition Competition { get; set; } = default!;
}
