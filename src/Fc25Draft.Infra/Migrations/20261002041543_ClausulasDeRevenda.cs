using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class ClausulasDeRevenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClausulasRevenda",
                columns: table => new
                {
                    ClausulaId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    BeneficiarioTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    DevedorTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Percentual = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: true),
                    CriadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EncerradaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ValorPago = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClausulasRevenda", x => x.ClausulaId);
                    table.ForeignKey(
                        name: "FK_ClausulasRevenda_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClausulasRevenda_Teams_BeneficiarioTeamId",
                        column: x => x.BeneficiarioTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClausulasRevenda_Teams_DevedorTeamId",
                        column: x => x.DevedorTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClausulasRevenda_BeneficiarioTeamId",
                table: "ClausulasRevenda",
                column: "BeneficiarioTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ClausulasRevenda_DevedorTeamId",
                table: "ClausulasRevenda",
                column: "DevedorTeamId");

            // Vendas já aceitas com percentual de revenda cujo jogador continua com quem comprou: a cláusula
            // passa a valer. (Quem já foi vendido de novo não entra — essa revenda não foi paga na época.)
            migrationBuilder.Sql("""
                INSERT INTO "ClausulasRevenda" ("ClausulaId", "PlayerId", "BeneficiarioTeamId", "DevedorTeamId", "Percentual", "OfferId", "CriadaEm")
                SELECT gen_random_uuid(), op."PlayerId",
                       CASE WHEN op."IsTarget" THEN o."ToTeamId" ELSE o."FromTeamId" END,
                       CASE WHEN op."IsTarget" THEN o."FromTeamId" ELSE o."ToTeamId" END,
                       o."SellOnPercentage", o."OfferId", o."UpdatedAtUtc"
                FROM "TransferOffers" o
                JOIN "TransferOfferPlayers" op ON op."OfferId" = o."OfferId"
                WHERE o."Status" = 2 AND o."SellOnPercentage" > 0 AND o."Type" <> 2
                  AND EXISTS (SELECT 1 FROM "TeamRosters" r
                              WHERE r."PlayerId" = op."PlayerId"
                                AND r."TeamId" = CASE WHEN op."IsTarget" THEN o."FromTeamId" ELSE o."ToTeamId" END);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_ClausulasRevenda_PlayerId_EncerradaEm",
                table: "ClausulasRevenda",
                columns: new[] { "PlayerId", "EncerradaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClausulasRevenda");
        }
    }
}
