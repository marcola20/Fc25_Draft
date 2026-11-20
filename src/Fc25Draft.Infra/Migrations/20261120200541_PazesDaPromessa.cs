using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PazesDaPromessa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PazesEm",
                table: "TeamRosters",
                type: "timestamp without time zone",
                nullable: true);

            // Promessas já cumpridas: a data é a do aviso "Promessa cumprida" (o mais recente de cada jogador no time).
            migrationBuilder.Sql("""
                UPDATE "TeamRosters" r
                SET "PazesEm" = a.quando
                FROM (
                    SELECT "TeamId", "Link", MAX("CriadoEm") AS quando
                    FROM "AvisosTimes"
                    WHERE "Tipo" = 'PROMESSA'
                    GROUP BY "TeamId", "Link"
                ) a
                WHERE a."TeamId" = r."TeamId"
                  AND a."Link" = '/players/details/' || r."PlayerId";
                """);

            // Quem voltou à lista depois da promessa cumprida sem ter passado 3 jogos do clube desde então (uma venda no
            // elenco mudou a conta do humor e os jogos de antes da promessa contaram): sai da lista e o time é avisado.
            // Se a promessa foi dada como cumprida sem nenhum jogo do clube desde o pedido para sair (foi a conta do
            // humor que virou, não ele que jogou), ela volta a estar em andamento, como na regra nova: titular 2 jogos
            // seguidos nos 5 jogos dela. Senão, as pazes valem e ele só pede de novo com mais 3 jogos insatisfeito.
            migrationBuilder.Sql("""
                WITH jogos AS (
                    SELECT DISTINCT e."TimeId", e."PartidaId", COALESCE(p."EncerradaEm", rd."DataHora") AS data
                    FROM "LigaEscalacoesPartida" e
                    JOIN "LigaPartidas" p ON p."PartidaId" = e."PartidaId"
                    JOIN "LigaRodadas" rd ON rd."RodadaId" = p."RodadaId"
                    WHERE e."Titular" AND p."Status" = 2
                ),
                indevidos AS (
                    SELECT r."TeamId", r."PlayerId", r."PazesEm",
                           NOT EXISTS (
                               SELECT 1 FROM jogos j
                               WHERE j."TimeId" = r."TeamId"
                                 AND j.data <= r."PazesEm"
                                 AND j.data > COALESCE((
                                     SELECT MAX(a."CriadoEm") FROM "AvisosTimes" a
                                     WHERE a."TeamId" = r."TeamId" AND a."Tipo" = 'PEDIU_PARA_SAIR'
                                       AND a."Link" = '/players/details/' || r."PlayerId"
                                       AND a."CriadoEm" < r."PazesEm"), r."PazesEm")
                           ) AND EXISTS (
                               SELECT 1 FROM "AvisosTimes" a
                               WHERE a."TeamId" = r."TeamId" AND a."Tipo" = 'PEDIU_PARA_SAIR'
                                 AND a."Link" = '/players/details/' || r."PlayerId"
                                 AND a."CriadoEm" < r."PazesEm"
                           ) AS sem_jogo
                    FROM "TeamRosters" r
                    WHERE r."SaidaPedidaEm" IS NOT NULL
                      AND r."PazesEm" IS NOT NULL
                      AND r."SaidaPedidaEm" > r."PazesEm"
                      AND NOT r."PromessaQuebrada"
                      AND (
                          SELECT COUNT(*) FROM jogos j
                          WHERE j."TimeId" = r."TeamId" AND j.data > r."PazesEm" AND j.data <= r."SaidaPedidaEm"
                      ) < 3
                ),
                avisos AS (
                    INSERT INTO "AvisosTimes" ("AvisoId", "TeamId", "Tipo", "Texto", "Link", "CriadoEm")
                    SELECT gen_random_uuid(), i."TeamId", 'PROMESSA',
                           CASE WHEN i.sem_jogo
                               THEN '🤝 Correção: ' || pl."Name" || ' saiu da lista de transferências e a promessa de colocar '
                                    || 'para jogar voltou a valer: a promessa tinha sido dada como cumprida sem ele jogar. '
                                    || 'Ele precisa ser titular 2 jogos seguidos nos próximos 5 jogos do clube.'
                               ELSE '🤝 Correção: ' || pl."Name" || ' saiu da lista de transferências. Ele tinha feito as pazes com a promessa '
                                    || 'cumprida e voltou à lista por engano, pelos jogos de antes dela. Só pede para sair de novo se ficar '
                                    || 'insatisfeito por mais 3 jogos.'
                           END,
                           '/players/details/' || i."PlayerId", NOW() AT TIME ZONE 'UTC'
                    FROM indevidos i
                    JOIN "Players" pl ON pl."PlayerId" = i."PlayerId"
                )
                UPDATE "TeamRosters" r
                SET "SaidaPedidaEm" = NULL,
                    "VariacaoCongelada" = NULL,
                    "AskingPrice" = NULL,
                    "ListedAtUtc" = NULL,
                    "PromessaEm" = CASE WHEN i.sem_jogo THEN i."PazesEm" ELSE NULL END,
                    "PazesEm" = CASE WHEN i.sem_jogo THEN NULL ELSE i."PazesEm" END
                FROM indevidos i
                WHERE r."TeamId" = i."TeamId" AND r."PlayerId" = i."PlayerId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PazesEm",
                table: "TeamRosters");
        }
    }
}
