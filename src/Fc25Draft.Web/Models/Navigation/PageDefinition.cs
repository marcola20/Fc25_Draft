using System.Collections.Generic;

namespace Fc25Draft.Web.Models.Navigation;

public class PageDefinition
{
    public string Route { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Subtitle { get; init; }
    public IReadOnlyList<BreadcrumbSegment> Breadcrumbs { get; init; } = new List<BreadcrumbSegment>();
}
