using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Extensions;

public static class TransferTypeExtensions
{
    public static string ToDisplayName(this TransferType type) => type switch
    {
        TransferType.Auction => "Leilão",
        TransferType.BuyNow => "Compra imediata",
        TransferType.Sale => "Venda",
        TransferType.Swap => "Troca",
        TransferType.AdminMove => "Movimentação administrativa",
        _ => type.ToString()
    };
}
