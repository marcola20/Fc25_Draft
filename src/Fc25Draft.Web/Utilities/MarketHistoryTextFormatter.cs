using Fc25Draft.Core.DTOs;
using Fc25Draft.Core.Enums;

namespace Fc25Draft.Web.Utilities;

public static class MarketHistoryTextFormatter
{
    public static string FormatDescription(MarketTransactionDto entry)
        => BuildMessage(entry);

    public static string FormatObservation(MarketTransactionDto entry)
        => BuildMessage(entry);

    private static string BuildMessage(MarketTransactionDto entry)
    {
        var origin = FormatTeam(entry.TeamName, "Mercado");
        var destination = FormatTeam(entry.TargetTeamName);
        var amountText = FormatAmount(entry.Amount);

        return entry.Type switch
        {
            MarketTransactionType.BidPlaced when amountText is not null
                => $"Lance de {amountText} por {origin}.",
            MarketTransactionType.Outbid when amountText is not null
                => $"{origin} superou {destination} com {amountText}.",
            MarketTransactionType.BuyNow when amountText is not null
                => $"Compra imediata por {origin} ({amountText}).",
            MarketTransactionType.AuctionSettled
                => $"Leilão concluído para {destination}.",
            MarketTransactionType.AuctionExpired
                => "Leilão expirado sem vencedor.",
            _ when !string.IsNullOrWhiteSpace(destination)
                => $"{origin} → {destination}",
            _ => origin
        };
    }

    private static string FormatTeam(string? name, string fallback = "-")
        => string.IsNullOrWhiteSpace(name) ? fallback : name!;

    private static string? FormatAmount(decimal? amount)
        => amount.HasValue ? string.Format(BrazilTime.Culture, "{0:C}", amount.Value) : null;
}
