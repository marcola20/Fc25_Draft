using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamLineupChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeamLineupChangeLogs",
                columns: table => new
                {
                    ChangeLogId = table.Column<Guid>(type: "uuid", nullable: false),
                    LineupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ChangesJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamLineupChangeLogs", x => x.ChangeLogId);
                    table.ForeignKey(
                        name: "FK_TeamLineupChangeLogs_TeamLineups_LineupId",
                        column: x => x.LineupId,
                        principalTable: "TeamLineups",
                        principalColumn: "LineupId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LineupChangeLog_Lineup_ChangedAt",
                table: "TeamLineupChangeLogs",
                columns: new[] { "LineupId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeamLineupChangeLogs");
        }
    }
}
