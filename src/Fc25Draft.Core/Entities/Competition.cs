namespace Fc25Draft.Core.Entities;

public sealed class Competition
{
    public Guid CompetitionId { get; set; }
    public Guid SeasonId { get; set; }

    public string Name { get; set; } = default!;
    public int Order { get; set; }
    public bool IsActive { get; set; } = true;

    public Season Season { get; set; } = default!;
    public ICollection<Round> Rounds { get; set; } = new List<Round>();
}
