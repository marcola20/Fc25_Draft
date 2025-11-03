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
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    OfferedFee = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    ResponseMessage = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferOffers", x => x.OfferId);
                    table.ForeignKey(
                        name: "FK_TransferOffers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferOffers_Teams_FromTeamId",
                        column: x => x.FromTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferOffers_Teams_ToTeamId",
                        column: x => x.ToTeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransferOfferSwapPlayers",
                columns: table => new
                {
                    SwapPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferOfferSwapPlayers", x => x.SwapPlayerId);
                    table.ForeignKey(
                        name: "FK_TransferOfferSwapPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferOfferSwapPlayers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferOfferSwapPlayers_TransferOffers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "TransferOffers",
                        principalColumn: "OfferId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransferOfferSwapPlayers_OfferId_PlayerId",
                table: "TransferOfferSwapPlayers",
                columns: new[] { "OfferId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransferOfferSwapPlayers_TeamId",
                table: "TransferOfferSwapPlayers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_FromTeamId",
                table: "TransferOffers",
                column: "FromTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_PlayerId_Status",
                table: "TransferOffers",
                columns: new[] { "PlayerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferOffers_ToTeam_Status_CreatedAtUtc",
                table: "TransferOffers",
                columns: new[] { "ToTeamId", "Status", "CreatedAtUtc" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferOfferSwapPlayers");

            migrationBuilder.DropTable(
                name: "TransferOffers");
        }
    }
}
