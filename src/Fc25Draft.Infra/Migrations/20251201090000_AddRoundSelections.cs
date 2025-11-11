using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddRoundSelections : Migration
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoundSelectionPlayers", x => new { x.RoundSelectionId, x.PlayerId });
                    table.ForeignKey(
                        name: "FK_RoundSelectionPlayers_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoundSelectionPlayers_RoundSelections_RoundSelectionId",
                        column: x => x.RoundSelectionId,
                        principalTable: "RoundSelections",
                        principalColumn: "RoundSelectionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoundSelectionPlayers_PlayerId",
                table: "RoundSelectionPlayers",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_RoundSelections_RoundId",
                table: "RoundSelections",
                column: "RoundId",
                unique: true);
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
