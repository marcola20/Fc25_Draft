namespace Fc25Draft.Web.Models.Market
{
    public class MarketQueryVm
    {
        public string? Name { get; set; }
        public List<int> Positions { get; set; } = new();
        public int? OverallMin { get; set; }
        public int? OverallMax { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Sort { get; set; }
    }
}
