using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTransferOffers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TransferOffers",
                columns: table => new
                {
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToTeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Money = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    SellOnPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    Clauses = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ParentOfferId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferOffers", x => x.OfferId);
                    table.ForeignKey(
                        name: "FK_TransferOffers_Teams_FromTeamId",
                        column: x => x.FromTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TransferOffers_Teams_ToTeamId",
                        column: x => x.ToTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId");
                    table.ForeignKey(
                        name: "FK_TransferOffers_TransferOffers_ParentOfferId",
                        column: x => x.ParentOfferId,
                        principalTable: "TransferOffers",
                        principalColumn: "OfferId");
                });

            migrationBuilder.CreateTable(
                name: "TransferOfferPlayers",
                columns: table => new
                {
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    IsTarget = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferOfferPlayers", x => new { x.OfferId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_TransferOfferPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferOfferPlayers_TransferOffers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "TransferOffers",
                        principalColumn: "OfferId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferOfferPlayers_PlayerId",
                table: "TransferOfferPlayers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_CreatedAtUtc",
                table: "TransferOffers",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_FromTeamId",
                table: "TransferOffers",
                column: "FromTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_ParentOfferId",
                table: "TransferOffers",
                column: "ParentOfferId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_Status",
                table: "TransferOffers",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_ToTeamId",
                table: "TransferOffers",
                column: "ToTeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferOfferPlayers");

            migrationBuilder.DropTable(
                name: "TransferOffers");
        }
    }
}
