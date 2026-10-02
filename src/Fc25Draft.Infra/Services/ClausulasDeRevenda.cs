using System.Globalization;
using Fc25Draft.Core.Entities;
using Fc25Draft.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace Fc25Draft.Infra.Services;

/// <summary>
/// Percentual de revenda (<see cref="ClausulaRevenda"/>): nasce numa venda aceita com percentual e é paga
/// na próxima venda do jogador — sobre o valor total, por quem vendeu, ao clube antigo. Não salva.
/// </summary>
internal static class ClausulasDeRevenda
{
    public const string OrigemExtrato = "REVENDA";
    private static readonly CultureInfo Br = CultureInfo.GetCultureInfo("pt-BR");

    public static void Criar(DraftDbContext db, int playerId, Guid beneficiarioId, Guid devedorId, decimal percentual, Guid? offerId, DateTime quando)
    {
        if (percentual <= 0 || beneficiarioId == devedorId) return;
        db.ClausulasRevenda.Add(new ClausulaRevenda
        {
            ClausulaId = Guid.NewGuid(),
            PlayerId = playerId,
            BeneficiarioTeamId = beneficiarioId,
            DevedorTeamId = devedorId,
            Percentual = percentual,
            OfferId = offerId,
            CriadaEm = quando
        });
    }

    /// <summary>
    /// O jogador saiu do <paramref name="vendedor"/> por <paramref name="valorDaVenda"/>: paga as cláusulas em que
    /// o vendedor é o devedor e encerra todas as cláusulas abertas do jogador.
    /// </summary>
    public static async Task PagarAsync(
        DraftDbContext db, int playerId, string jogadorNome, Team vendedor, decimal valorDaVenda, string comoSaiu, DateTime quando, CancellationToken ct)
    {
        var abertas = await db.ClausulasRevenda
            .Where(c => c.PlayerId == playerId && c.EncerradaEm == null)
            .ToListAsync(ct);

        foreach (var clausula in abertas)
        {
            clausula.EncerradaEm = quando;
            clausula.ValorPago = 0m;
            if (clausula.DevedorTeamId != vendedor.TeamId || valorDaVenda <= 0) continue;

            var valor = decimal.Round(valorDaVenda * clausula.Percentual / 100m, 2, MidpointRounding.AwayFromZero);
            if (valor <= 0) continue;

            var beneficiario = await db.Teams.FirstAsync(t => t.TeamId == clausula.BeneficiarioTeamId, ct);
            vendedor.Budget = decimal.Round(vendedor.Budget - valor, 2, MidpointRounding.AwayFromZero);
            beneficiario.Budget = decimal.Round(beneficiario.Budget + valor, 2, MidpointRounding.AwayFromZero);
            clausula.ValorPago = valor;

            var pct = clausula.Percentual.ToString("0.##", Br);
            var descricao = $"Revenda de {pct}% de {jogadorNome} ({comoSaiu} por {valorDaVenda.ToString("C0", Br)}): {vendedor.TeamName} paga ao {beneficiario.TeamName}";
            ExtratoCaixa.Lancar(db, vendedor.TeamId, -valor, OrigemExtrato, descricao, quando);
            ExtratoCaixa.Lancar(db, beneficiario.TeamId, valor, OrigemExtrato, descricao, quando);
            AvisosDoTime.Criar(db, beneficiario.TeamId, AvisosDoTime.Revenda,
                $"Você recebeu {valor.ToString("C0", Br)} de revenda: o {vendedor.TeamName} vendeu {jogadorNome} ({pct}%).",
                $"/teams/details/{beneficiario.TeamId}?aba=extrato", quando);
        }
    }
}
