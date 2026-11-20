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
            migrationBuilder.Sql("""
                WITH indevidos AS (
                    SELECT r."TeamId", r."PlayerId"
                    FROM "TeamRosters" r
                    WHERE r."SaidaPedidaEm" IS NOT NULL
                      AND r."PazesEm" IS NOT NULL
                      AND r."SaidaPedidaEm" > r."PazesEm"
                      AND NOT r."PromessaQuebrada"
                      AND (
                          SELECT COUNT(DISTINCT e."PartidaId")
                          FROM "LigaEscalacoesPartida" e
                          JOIN "LigaPartidas" p ON p."PartidaId" = e."PartidaId"
                          JOIN "LigaRodadas" rd ON rd."RodadaId" = p."RodadaId"
                          WHERE e."TimeId" = r."TeamId"
                            AND e."Titular"
                            AND p."Status" = 2
                            AND COALESCE(p."EncerradaEm", rd."DataHora") > r."PazesEm"
                            AND COALESCE(p."EncerradaEm", rd."DataHora") <= r."SaidaPedidaEm"
                      ) < 3
                ),
                avisos AS (
                    INSERT INTO "AvisosTimes" ("AvisoId", "TeamId", "Tipo", "Texto", "Link", "CriadoEm")
                    SELECT gen_random_uuid(), i."TeamId", 'PROMESSA',
                           '🤝 Correção: ' || pl."Name" || ' saiu da lista de transferências. Ele tinha feito as pazes com a promessa '
                           || 'cumprida e voltou à lista por engano, pelos jogos de antes dela. Só pede para sair de novo se ficar '
                           || 'insatisfeito por mais 3 jogos.',
                           '/players/details/' || i."PlayerId", NOW() AT TIME ZONE 'UTC'
                    FROM indevidos i
                    JOIN "Players" pl ON pl."PlayerId" = i."PlayerId"
                )
                UPDATE "TeamRosters" r
                SET "SaidaPedidaEm" = NULL,
                    "VariacaoCongelada" = NULL,
                    "AskingPrice" = NULL,
                    "ListedAtUtc" = NULL
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
