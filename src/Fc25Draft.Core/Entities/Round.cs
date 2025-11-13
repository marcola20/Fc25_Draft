namespace Fc25Draft.Core.Entities;

public sealed class Round
{
    public Guid RoundId { get; set; }
    public Guid CompetitionId { get; set; }

    public string Name { get; set; } = default!;
    public int RoundNumber { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? PlayedAtUtc { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public Competition Competition { get; set; } = default!;
    public RoundSelection? Selection { get; set; }
    public ICollection<CompetitionMatch> Matches { get; set; } = new List<CompetitionMatch>();
}
