using Fc25Draft.Core.Enums;

namespace Fc25Draft.Core.Extensions;

public static class MarketTransactionTypeExtensions
{
    public static string ToDisplayName(this MarketTransactionType type) => type switch
    {
        MarketTransactionType.BidPlaced => "Lance registrado",
        MarketTransactionType.Outbid => "Lance superado",
        MarketTransactionType.BuyNow => "Compra imediata",
        MarketTransactionType.AuctionSettled => "Leilão concluído",
        MarketTransactionType.AuctionExpired => "Leilão expirado",
        MarketTransactionType.ItemCanceled => "Item cancelado",
        _ => "Atualização do mercado"
    };
}
