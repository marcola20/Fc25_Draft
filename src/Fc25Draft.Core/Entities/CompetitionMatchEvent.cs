using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Entities;

public sealed class CompetitionMatchEvent
{
    public Guid CompetitionMatchEventId { get; set; }
    public Guid CompetitionMatchId { get; set; }
    public Guid CompetitionTeamId { get; set; }
    public int? PlayerId { get; set; }
    public int? RelatedPlayerId { get; set; }
    public CompetitionMatchEventType EventType { get; set; }
    public int? Minute { get; set; }
    public string? Observations { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public CompetitionMatch Match { get; set; } = default!;
    public CompetitionTeam Team { get; set; } = default!;
    public Player? Player { get; set; }
    public Player? RelatedPlayer { get; set; }
}
