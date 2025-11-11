namespace Fc25Draft.Core.Entities;

public sealed class Season
{
    public Guid SeasonId { get; set; }
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    public ICollection<Competition> Competitions { get; set; } = new List<Competition>();
    public ICollection<SeasonScheduleItem> Schedule { get; set; } = new List<SeasonScheduleItem>();
}
