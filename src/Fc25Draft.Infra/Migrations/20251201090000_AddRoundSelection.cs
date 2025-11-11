using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoundSelections",
                columns: table => new
                {
                    RoundSelectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoundId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundSelections", x => x.RoundSelectionId);
                    table.ForeignKey(
                        name: "FK_RoundSelections_Rounds_RoundId",
                        column: x => x.RoundId,
                        principalTable: "Rounds",
                        principalColumn: "RoundId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoundSelectionPlayers",
                columns: table => new
                {
                    RoundSelectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerGuid = table.Column<Guid>(type: "uuid", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundSelectionPlayers", x => new { x.RoundSelectionId, x.PlayerGuid });
                    table.ForeignKey(
                        name: "FK_RoundSelectionPlayers_Players_PlayerGuid",
                        column: x => x.PlayerGuid,
                        principalTable: "Players",
                        principalColumn: "PlayerGuid",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoundSelectionPlayers_RoundSelections_RoundSelectionId",
                        column: x => x.RoundSelectionId,
                        principalTable: "RoundSelections",
                        principalColumn: "RoundSelectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoundSelections_RoundId",
                table: "RoundSelections",
                column: "RoundId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoundSelectionPlayers_PlayerGuid",
                table: "RoundSelectionPlayers",
                column: "PlayerGuid");

            migrationBuilder.CreateIndex(
                name: "IX_RoundSelectionPlayers_RoundSelectionId",
                table: "RoundSelectionPlayers",
                column: "RoundSelectionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoundSelectionPlayers");

            migrationBuilder.DropTable(
                name: "RoundSelections");
        }
    }
}
