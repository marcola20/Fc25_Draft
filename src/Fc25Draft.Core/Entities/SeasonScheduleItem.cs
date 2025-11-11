namespace Fc25Draft.Core.Entities;

public sealed class SeasonScheduleItem
{
    public Guid SeasonScheduleItemId { get; set; }
    public Guid SeasonId { get; set; }

    public int Order { get; set; }
    public Guid RoundId { get; set; }

    public Season Season { get; set; } = default!;
    public Round Round { get; set; } = default!;
}
