using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamLineups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamLineups",
                columns: table => new
                {
                    LineupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Formation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLineups", x => x.LineupId);
                    table.ForeignKey(
                        name: "FK_TeamLineups_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamLineupSlots",
                columns: table => new
                {
                    LineupSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsBench = table.Column<bool>(type: "boolean", nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLineupSlots", x => x.LineupSlotId);
                    table.ForeignKey(
                        name: "FK_TeamLineupSlots_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeamLineupSlots_TeamLineups_LineupId",
                        column: x => x.LineupId,
                        principalTable: "TeamLineups",
                        principalColumn: "LineupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_TeamId",
                table: "TeamLineups",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Lineup_Team_Active",
                table: "TeamLineups",
                columns: new[] { "TeamId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineupSlots_LineupId_SlotCode",
                table: "TeamLineupSlots",
                columns: new[] { "LineupId", "SlotCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineupSlots_PlayerId",
                table: "TeamLineupSlots",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamLineupSlots");

            migrationBuilder.DropTable(
                name: "TeamLineups");
        }
    }
}
