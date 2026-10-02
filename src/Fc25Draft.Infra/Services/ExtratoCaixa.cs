using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Lançamento no extrato do time (<see cref="BudgetLedger"/>), gravado junto de toda mudança de caixa.
/// Não salva: entra no mesmo SaveChanges (e na mesma transação) da operação que mexeu no saldo.
/// </summary>
internal static class ExtratoCaixa
{
    // Origens do extrato (além de PREMIACAO, DRAFT_EXPANSAO e EXPANSAO_CAIXA_INICIAL, gravadas em outros lugares).
    public const string Transferencia = "TRANSFERENCIA";
    public const string Troca = "TROCA";
    public const string Emprestimo = "EMPRESTIMO";
    public const string OpcaoDeCompra = "OPCAO_COMPRA";
    public const string Leilao = "LEILAO";
    public const string CompraImediata = "COMPRA_IMEDIATA";
    public const string VendaRapida = "VENDA_RAPIDA";
    public const string AjusteAdmin = "AJUSTE_ADMIN";
    public const string VendaAdmin = "VENDA_ADMIN";
    public const string TrocaAdmin = "TROCA_ADMIN";

    /// <summary><paramref name="valor"/> com sinal: positivo entrou no caixa, negativo saiu.</summary>
    public static void Lancar(DraftDbContext db, Guid teamId, decimal valor, string origem, string descricao, DateTime quando)
    {
        valor = decimal.Round(valor, 2, MidpointRounding.AwayFromZero);
        if (valor == 0m) return;

        db.BudgetLedgers.Add(new BudgetLedger
        {
            BudgetLedgerId = Guid.NewGuid(),
            TeamId = teamId,
            DataUtc = quando,
            Tipo = valor > 0 ? "CREDIT" : "DEBIT",
            Origem = origem,
            Valor = Math.Abs(valor),
            Descricao = descricao.Length > 256 ? descricao[..253] + "..." : descricao
        });
    }
}
