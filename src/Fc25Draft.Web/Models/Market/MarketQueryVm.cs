using System.Collections.Generic;

namespace Fc25Draft.Web.Models.Market;

public class MarketQueryVm
{
    public string? SearchTerm { get; set; }

    public List<int> PositionIds { get; set; } = new();

    public int? OverallMin { get; set; }

    public int? OverallMax { get; set; }

    public string? Status { get; set; }

    public string? SortBy { get; set; }

    public string? SortOrder { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
