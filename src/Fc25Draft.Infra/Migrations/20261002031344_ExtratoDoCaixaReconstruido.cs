using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class ExtratoDoCaixaReconstruido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O extrato do caixa (BudgetLedgers) só tinha premiação e draft de expansão. Reconstrói o
            // passado a partir do histórico de transferências; daqui para frente cada operação grava o seu
            // lançamento. Ajuste manual do admin não deixou rastro: entra no "saldo anterior" do extrato.
            migrationBuilder.Sql("""
                INSERT INTO "BudgetLedgers" ("BudgetLedgerId", "TeamId", "DataUtc", "Tipo", "Origem", "Valor", "Descricao")

                -- Leilão e compra imediata: quem levou o jogador pagou.
                SELECT gen_random_uuid(), h."ToTeamId", h."PerformedAtUtc", 'DEBIT',
                       CASE WHEN h."Notes" ILIKE 'Compra imediata%' THEN 'COMPRA_IMEDIATA' ELSE 'LEILAO' END,
                       h."Amount",
                       left(CASE WHEN h."Notes" ILIKE 'Compra imediata%' THEN 'Compra imediata: ' ELSE 'Leilão: ' END || p."Name", 256)
                FROM "TransferHistories" h
                JOIN "Players" p ON p."PlayerId" = h."PlayerId"
                WHERE h."Type" = 1 AND h."Amount" > 0 AND h."ToTeamId" IS NOT NULL

                UNION ALL

                -- Venda rápida: o time recebeu.
                SELECT gen_random_uuid(), h."FromTeamId", h."PerformedAtUtc", 'CREDIT', 'VENDA_RAPIDA',
                       h."Amount", left('Venda rápida de ' || p."Name", 256)
                FROM "TransferHistories" h
                JOIN "Players" p ON p."PlayerId" = h."PlayerId"
                WHERE h."Type" = 4 AND h."Amount" > 0 AND h."FromTeamId" IS NOT NULL

                UNION ALL

                -- Opção de compra do empréstimo: o tomador (destino) paga o dono (origem).
                SELECT gen_random_uuid(), x."TimeId", h."PerformedAtUtc", x."Tipo", 'OPCAO_COMPRA', h."Amount", left(h."Notes", 256)
                FROM "TransferHistories" h
                CROSS JOIN LATERAL (VALUES (h."ToTeamId", 'DEBIT'), (h."FromTeamId", 'CREDIT')) AS x("TimeId", "Tipo")
                WHERE h."Type" = 8 AND h."Amount" > 0 AND h."FromTeamId" IS NOT NULL AND h."ToTeamId" IS NOT NULL

                UNION ALL

                -- Venda pela organização: o comprador (destino) paga o vendedor (origem).
                SELECT gen_random_uuid(), x."TimeId", h."PerformedAtUtc", x."Tipo", 'VENDA_ADMIN', h."Amount", left(h."Notes", 256)
                FROM "TransferHistories" h
                CROSS JOIN LATERAL (VALUES (h."ToTeamId", 'DEBIT'), (h."FromTeamId", 'CREDIT')) AS x("TimeId", "Tipo")
                WHERE h."Type" = 2 AND h."Amount" > 0 AND h."PerformedBy" <> 'system'
                  AND h."FromTeamId" IS NOT NULL AND h."ToTeamId" IS NOT NULL

                UNION ALL

                -- Propostas aceitas (venda, troca, empréstimo): as notas dizem quem pagou ("<time> paga R$ ...").
                SELECT gen_random_uuid(),
                       CASE WHEN x."Tipo" = 'DEBIT' THEN t."Pagador" ELSE t."Recebedor" END,
                       t."PerformedAtUtc", x."Tipo",
                       CASE t."Type" WHEN 3 THEN 'TROCA' WHEN 6 THEN 'EMPRESTIMO' ELSE 'TRANSFERENCIA' END,
                       t."Amount", left(t."Notes", 256)
                FROM (
                    SELECT h.*,
                           CASE WHEN position(f."TeamName" || ' paga ' IN h."Notes") > 0 THEN h."FromTeamId"
                                WHEN position(d."TeamName" || ' paga ' IN h."Notes") > 0 THEN h."ToTeamId" END AS "Pagador",
                           CASE WHEN position(f."TeamName" || ' paga ' IN h."Notes") > 0 THEN h."ToTeamId"
                                WHEN position(d."TeamName" || ' paga ' IN h."Notes") > 0 THEN h."FromTeamId" END AS "Recebedor"
                    FROM "TransferHistories" h
                    JOIN "Teams" f ON f."TeamId" = h."FromTeamId"
                    JOIN "Teams" d ON d."TeamId" = h."ToTeamId"
                    WHERE h."Type" IN (2, 3, 6) AND h."Amount" > 0 AND h."PerformedBy" = 'system'
                ) t
                CROSS JOIN (VALUES ('DEBIT'), ('CREDIT')) AS x("Tipo")
                WHERE t."Pagador" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM "BudgetLedgers"
                WHERE "Origem" IN ('LEILAO', 'COMPRA_IMEDIATA', 'VENDA_RAPIDA', 'OPCAO_COMPRA', 'VENDA_ADMIN',
                                   'TRANSFERENCIA', 'TROCA', 'EMPRESTIMO');
                """);
        }
    }
}
