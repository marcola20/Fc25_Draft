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
        var player = string.IsNullOrWhiteSpace(entry.PlayerName) ? null : entry.PlayerName;
        var amountText = FormatAmount(entry.Amount);

        return entry.Type switch
        {
            MarketTransactionType.BidPlaced when amountText is not null
                => player is null
                    ? $"Lance de {amountText} por {origin}."
                    : $"Lance de {amountText} por {origin} em {player}.",
            MarketTransactionType.Outbid when amountText is not null
                => player is null
                    ? $"{origin} superou {destination} com {amountText}."
                    : $"{origin} superou {destination} com {amountText} em {player}.",
            MarketTransactionType.BuyNow when amountText is not null
                => player is null
                    ? $"Compra imediata por {origin} ({amountText})."
                    : $"Compra imediata por {origin} em {player} ({amountText}).",
            MarketTransactionType.AuctionSettled when player is not null && amountText is not null
                => $"Leilão concluído: {player} para {destination} por {amountText}.",
            MarketTransactionType.AuctionSettled when amountText is not null
                => $"Leilão concluído para {destination} por {amountText}.",
            MarketTransactionType.AuctionSettled
                => $"Leilão concluído para {destination}.",
            MarketTransactionType.AuctionExpired when player is not null
                => $"Leilão de {player} expirou sem vencedor.",
            MarketTransactionType.AuctionExpired
                => "Leilão expirou sem vencedor.",
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
