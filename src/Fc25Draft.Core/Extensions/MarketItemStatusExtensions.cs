using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Extensions;

public static class MarketItemStatusExtensions
{
    public static string ToDisplayName(this MarketItemStatus status) => status switch
    {
        MarketItemStatus.Draft => "Rascunho",
        MarketItemStatus.Active => "Ativo",
        MarketItemStatus.Sold => "Vendido",
        MarketItemStatus.Canceled => "Cancelado",
        MarketItemStatus.Expired => "Expirado",
        _ => status.ToString()
    };
}
