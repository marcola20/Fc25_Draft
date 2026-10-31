using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PicksNegociaveis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PicksDoDraft",
                columns: table => new
                {
                    PickDoDraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    Rodada = table.Column<int>(type: "integer", nullable: false),
                    TimeOriginalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DonoId = table.Column<Guid>(type: "uuid", nullable: false),
                    NegociadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PicksDoDraft", x => x.PickDoDraftId);
                    table.ForeignKey(
                        name: "FK_PicksDoDraft_Teams_DonoId",
                        column: x => x.DonoId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PicksDoDraft_Teams_TimeOriginalId",
                        column: x => x.TimeOriginalId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TransferOfferPicks",
                columns: table => new
                {
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    Rodada = table.Column<int>(type: "integer", nullable: false),
                    TimeOriginalId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsTarget = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferOfferPicks", x => new { x.OfferId, x.Temporada, x.Rodada, x.TimeOriginalId });
                    table.ForeignKey(
                        name: "FK_TransferOfferPicks_Teams_TimeOriginalId",
                        column: x => x.TimeOriginalId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TransferOfferPicks_TransferOffers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "TransferOffers",
                        principalColumn: "OfferId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PicksDoDraft_DonoId",
                table: "PicksDoDraft",
                column: "DonoId");

            migrationBuilder.CreateIndex(
                name: "IX_PicksDoDraft_Temporada_Rodada_TimeOriginalId",
                table: "PicksDoDraft",
                columns: new[] { "Temporada", "Rodada", "TimeOriginalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PicksDoDraft_TimeOriginalId",
                table: "PicksDoDraft",
                column: "TimeOriginalId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOfferPicks_TimeOriginalId",
                table: "TransferOfferPicks",
                column: "TimeOriginalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PicksDoDraft");

            migrationBuilder.DropTable(
                name: "TransferOfferPicks");
        }
    }
}
