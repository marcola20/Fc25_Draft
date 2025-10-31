using System.Globalization;
using Fc25Draft.Core.Enums;

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
    public string? PerformedBy { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;

    public string ToQueryString(bool includePaging = true)
    {
        var parameters = new List<string>();

        if (CycleId.HasValue && CycleId.Value != Guid.Empty)
        {
            parameters.Add($"cycleId={CycleId.Value}");
        }

        if (ItemId.HasValue && ItemId.Value != Guid.Empty)
        {
            parameters.Add($"itemId={ItemId.Value}");
        }

        if (TeamId.HasValue && TeamId.Value != Guid.Empty)
        {
            parameters.Add($"teamId={TeamId.Value}");
        }

        if (PlayerId.HasValue)
        {
            parameters.Add($"playerId={PlayerId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(PlayerName))
        {
            parameters.Add($"playerName={Uri.EscapeDataString(PlayerName.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(TeamName))
        {
            parameters.Add($"teamName={Uri.EscapeDataString(TeamName.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(TargetTeamName))
        {
            parameters.Add($"targetTeamName={Uri.EscapeDataString(TargetTeamName.Trim())}");
        }

        if (Type.HasValue)
        {
            parameters.Add($"type={(int)Type.Value}");
        }

        if (!string.IsNullOrWhiteSpace(PerformedBy))
        {
            parameters.Add($"performedBy={Uri.EscapeDataString(PerformedBy.Trim())}");
        }

        if (FromUtc.HasValue)
        {
            parameters.Add($"from={Uri.EscapeDataString(ToIsoString(FromUtc.Value))}");
        }

        if (ToUtc.HasValue)
        {
            parameters.Add($"to={Uri.EscapeDataString(ToIsoString(ToUtc.Value))}");
        }

        if (includePaging)
        {
            parameters.Add($"page={Page}");
            parameters.Add($"pageSize={PageSize}");
        }

        return string.Join("&", parameters);
    }

    private static string ToIsoString(DateTime value)
    {
        if (value.Kind == DateTimeKind.Unspecified)
        {
            value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return value.Kind == DateTimeKind.Utc
            ? value.ToString("O", CultureInfo.InvariantCulture)
            : value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }
}
