using Fc25Draft.Core.Entities;

namespace Fc25Draft.Core.Extensions;

public static class TransferTypeExtensions
{
    public static string ToDisplayName(this TransferType type) => type switch
    {
        TransferType.MarketAuction => "Mercado",
        TransferType.TeamSale => "Venda entre times",
        TransferType.TeamTrade => "Troca entre times",
        TransferType.QuickSell => "Venda rápida",
        TransferType.ExpansionDraft => "Draft de expansão",
        TransferType.Loan => "Empréstimo",
        TransferType.LoanReturn => "Fim de empréstimo",
        TransferType.LoanPurchase => "Compra após empréstimo",
        TransferType.Aposentadoria => "Aposentadoria",
        _ => type.ToString()
    };
}
