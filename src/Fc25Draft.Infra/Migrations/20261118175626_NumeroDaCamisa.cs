using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class NumeroDaCamisa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "NumeroCamisa",
                table: "TeamRosters",
                type: "smallint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamRosters_TeamId_NumeroCamisa",
                table: "TeamRosters",
                columns: new[] { "TeamId", "NumeroCamisa" },
                unique: true,
                filter: "\"NumeroCamisa\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeamRosters_NumeroCamisa",
                table: "TeamRosters",
                sql: "\"NumeroCamisa\" BETWEEN 1 AND 99");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamRosters_TeamId_NumeroCamisa",
                table: "TeamRosters");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TeamRosters_NumeroCamisa",
                table: "TeamRosters");

            migrationBuilder.DropColumn(
                name: "NumeroCamisa",
                table: "TeamRosters");
        }
    }
}
