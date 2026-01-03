using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AllowPlayerReuseAcrossDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DraftPicks_PlayerId",
                table: "DraftPicks");

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_DraftId_PlayerId",
                table: "DraftPicks",
                columns: new[] { "DraftId", "PlayerId" },
                unique: true,
                filter: "\"PlayerId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DraftPicks_DraftId_PlayerId",
                table: "DraftPicks");

            migrationBuilder.CreateIndex(
                name: "IX_DraftPicks_PlayerId",
                table: "DraftPicks",
                column: "PlayerId",
                unique: true,
                filter: "\"PlayerId\" IS NOT NULL");
        }
    }
}
