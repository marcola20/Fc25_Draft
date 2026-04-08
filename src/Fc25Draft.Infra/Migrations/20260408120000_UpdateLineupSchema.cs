using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLineupSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove TacticCode
            migrationBuilder.DropColumn(
                name: "TacticCode",
                table: "TeamLineups");

            // Add AutoSubstitution
            migrationBuilder.AddColumn<int>(
                name: "AutoSubstitution",
                table: "TeamLineups",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Rename ShortFreeKickLeft -> ShortFreeKick1
            migrationBuilder.RenameColumn(
                name: "ShortFreeKickLeftPlayerId",
                table: "TeamLineups",
                newName: "ShortFreeKick1PlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamLineups_ShortFreeKickLeftPlayerId",
                table: "TeamLineups",
                newName: "IX_TeamLineups_ShortFreeKick1PlayerId");

            // Rename ShortFreeKickRight -> ShortFreeKick2
            migrationBuilder.RenameColumn(
                name: "ShortFreeKickRightPlayerId",
                table: "TeamLineups",
                newName: "ShortFreeKick2PlayerId");

            migrationBuilder.RenameIndex(
                name: "IX_TeamLineups_ShortFreeKickRightPlayerId",
                table: "TeamLineups",
                newName: "IX_TeamLineups_ShortFreeKick2PlayerId");

            // Add AttackingPlayer columns
            migrationBuilder.AddColumn<int>(
                name: "AttackingPlayer1Id",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttackingPlayer2Id",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AttackingPlayer3Id",
                table: "TeamLineups",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_AttackingPlayer1Id",
                table: "TeamLineups",
                column: "AttackingPlayer1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_AttackingPlayer2Id",
                table: "TeamLineups",
                column: "AttackingPlayer2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_AttackingPlayer3Id",
                table: "TeamLineups",
                column: "AttackingPlayer3Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TeamLineups_Players_AttackingPlayer1Id",
                table: "TeamLineups",
                column: "AttackingPlayer1Id",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamLineups_Players_AttackingPlayer2Id",
                table: "TeamLineups",
                column: "AttackingPlayer2Id",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamLineups_Players_AttackingPlayer3Id",
                table: "TeamLineups",
                column: "AttackingPlayer3Id",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);

            // Create TeamLineupOffensiveInstructions table
            migrationBuilder.CreateTable(
                name: "TeamLineupOffensiveInstructions",
                columns: table => new
                {
                    LineupId = table.Column<Guid>(type: "uuid", nullable: false),
                    OffensiveStyle = table.Column<int>(type: "integer", nullable: false),
                    Playmaker = table.Column<int>(type: "integer", nullable: false),
                    AttackArea = table.Column<int>(type: "integer", nullable: false),
                    Positioning = table.Column<int>(type: "integer", nullable: false),
                    SupportRange = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLineupOffensiveInstructions", x => x.LineupId);
                    table.ForeignKey(
                        name: "FK_TeamLineupOffensiveInstructions_TeamLineups_LineupId",
                        column: x => x.LineupId,
                        principalTable: "TeamLineups",
                        principalColumn: "LineupId",
                        onDelete: ReferentialAction.Cascade);
                });

            // Create TeamLineupDefensiveInstructions table
            migrationBuilder.CreateTable(
                name: "TeamLineupDefensiveInstructions",
                columns: table => new
                {
                    LineupId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefensiveStyle = table.Column<int>(type: "integer", nullable: false),
                    ContainmentArea = table.Column<int>(type: "integer", nullable: false),
                    Pressure = table.Column<int>(type: "integer", nullable: false),
                    DefensiveLine = table.Column<int>(type: "integer", nullable: false),
                    Density = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLineupDefensiveInstructions", x => x.LineupId);
                    table.ForeignKey(
                        name: "FK_TeamLineupDefensiveInstructions_TeamLineups_LineupId",
                        column: x => x.LineupId,
                        principalTable: "TeamLineups",
                        principalColumn: "LineupId",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TeamLineupOffensiveInstructions");
            migrationBuilder.DropTable(name: "TeamLineupDefensiveInstructions");

            migrationBuilder.DropForeignKey(name: "FK_TeamLineups_Players_AttackingPlayer1Id", table: "TeamLineups");
            migrationBuilder.DropForeignKey(name: "FK_TeamLineups_Players_AttackingPlayer2Id", table: "TeamLineups");
            migrationBuilder.DropForeignKey(name: "FK_TeamLineups_Players_AttackingPlayer3Id", table: "TeamLineups");

            migrationBuilder.DropIndex(name: "IX_TeamLineups_AttackingPlayer1Id", table: "TeamLineups");
            migrationBuilder.DropIndex(name: "IX_TeamLineups_AttackingPlayer2Id", table: "TeamLineups");
            migrationBuilder.DropIndex(name: "IX_TeamLineups_AttackingPlayer3Id", table: "TeamLineups");

            migrationBuilder.DropColumn(name: "AttackingPlayer1Id", table: "TeamLineups");
            migrationBuilder.DropColumn(name: "AttackingPlayer2Id", table: "TeamLineups");
            migrationBuilder.DropColumn(name: "AttackingPlayer3Id", table: "TeamLineups");
            migrationBuilder.DropColumn(name: "AutoSubstitution", table: "TeamLineups");

            migrationBuilder.RenameColumn(name: "ShortFreeKick1PlayerId", table: "TeamLineups", newName: "ShortFreeKickLeftPlayerId");
            migrationBuilder.RenameIndex(name: "IX_TeamLineups_ShortFreeKick1PlayerId", table: "TeamLineups", newName: "IX_TeamLineups_ShortFreeKickLeftPlayerId");
            migrationBuilder.RenameColumn(name: "ShortFreeKick2PlayerId", table: "TeamLineups", newName: "ShortFreeKickRightPlayerId");
            migrationBuilder.RenameIndex(name: "IX_TeamLineups_ShortFreeKick2PlayerId", table: "TeamLineups", newName: "IX_TeamLineups_ShortFreeKickRightPlayerId");

            migrationBuilder.AddColumn<string>(
                name: "TacticCode",
                table: "TeamLineups",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }
    }
}
