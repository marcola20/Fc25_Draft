using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeasonCalendarModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop Season/Calendar module tables (in dependency order)
            migrationBuilder.DropTable(name: "RoundSelectionPlayers");
            migrationBuilder.DropTable(name: "RoundSelections");
            migrationBuilder.DropTable(name: "SeasonSchedule");
            migrationBuilder.DropTable(name: "Rounds");
            migrationBuilder.DropTable(name: "Competitions");
            migrationBuilder.DropTable(name: "Seasons");

            migrationBuilder.DropIndex(
                name: "IX_TeamLineups_TeamId",
                table: "TeamLineups");

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_PlayerId",
                table: "DraftPicks",
                column: "PlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DraftPicks_PlayerId",
                table: "DraftPicks");

            migrationBuilder.CreateIndex(
                name: "IX_TeamLineups_TeamId",
                table: "TeamLineups",
                column: "TeamId");
        }
    }
}
