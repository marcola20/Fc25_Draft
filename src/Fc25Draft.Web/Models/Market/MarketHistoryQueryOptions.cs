using Fc25Draft.Core.Enums;
using Fc25Draft.Web.Utilities;

namespace Fc25Draft.Web.Models.Market;

public class MarketHistoryQueryOptions
{
    public Guid? CycleId { get; set; }
    public Guid? ItemId { get; set; }
    public Guid? TeamId { get; set; }
    public int? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public string? TeamName { get; set; }
    public string? TargetTeamName { get; set; }
    public MarketTransactionType? Type { get; set; }
    public bool OnlyAcquisitions { get; set; }
    public string? PerformedBy { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;

    public string ToQueryString(bool includePaging = true)
    {
        var builder = new QueryStringBuilder()
            .Add("cycleId", CycleId)
            .Add("itemId", ItemId)
            .Add("teamId", TeamId)
            .Add("playerId", PlayerId)
            .Add("playerName", Normalize(PlayerName))
            .Add("teamName", Normalize(TeamName))
            .Add("targetTeamName", Normalize(TargetTeamName))
            .Add("performedBy", Normalize(PerformedBy))
            .Add("from", FromUtc)
            .Add("to", ToUtc);

        if (Type.HasValue)
        {
            builder.AddInvariant("type", (int)Type.Value);
        }

        if (OnlyAcquisitions)
        {
            builder.Add("onlyAcquisitions", "true");
        }

        if (includePaging)
        {
            var safePage = Page < 1 ? 1 : Page;
            var safePageSize = PageSize < 1 ? 1 : PageSize;
            builder.AddInvariant("page", safePage);
            builder.AddInvariant("pageSize", safePageSize);
        }

        return builder.Build();
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
