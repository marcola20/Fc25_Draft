using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvancedInstructions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamLineupAdvancedInstructions",
                columns: table => new
                {
                    LineupId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attack1 = table.Column<int>(type: "integer", nullable: false),
                    AttackPlayer1Id = table.Column<int>(type: "integer", nullable: true),
                    Attack2 = table.Column<int>(type: "integer", nullable: false),
                    AttackPlayer2Id = table.Column<int>(type: "integer", nullable: true),
                    Defense1 = table.Column<int>(type: "integer", nullable: false),
                    DefensePlayer1Id = table.Column<int>(type: "integer", nullable: true),
                    Defense2 = table.Column<int>(type: "integer", nullable: false),
                    DefensePlayer2Id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLineupAdvancedInstructions", x => x.LineupId);
                    table.ForeignKey(
                        name: "FK_TeamLineupAdvancedInstructions_Players_AttackPlayer1Id",
                        column: x => x.AttackPlayer1Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamLineupAdvancedInstructions_Players_AttackPlayer2Id",
                        column: x => x.AttackPlayer2Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamLineupAdvancedInstructions_Players_DefensePlayer1Id",
                        column: x => x.DefensePlayer1Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamLineupAdvancedInstructions_Players_DefensePlayer2Id",
                        column: x => x.DefensePlayer2Id,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamLineupAdvancedInstructions_TeamLineups_LineupId",
                        column: x => x.LineupId,
                        principalTable: "TeamLineups",
                        principalColumn: "LineupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineupAdvancedInstructions_AttackPlayer1Id",
                table: "TeamLineupAdvancedInstructions",
                column: "AttackPlayer1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineupAdvancedInstructions_AttackPlayer2Id",
                table: "TeamLineupAdvancedInstructions",
                column: "AttackPlayer2Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineupAdvancedInstructions_DefensePlayer1Id",
                table: "TeamLineupAdvancedInstructions",
                column: "DefensePlayer1Id");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineupAdvancedInstructions_DefensePlayer2Id",
                table: "TeamLineupAdvancedInstructions",
                column: "DefensePlayer2Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamLineupAdvancedInstructions");
        }
    }
}
