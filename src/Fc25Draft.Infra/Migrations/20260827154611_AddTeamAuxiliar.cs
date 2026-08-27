using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamAuxiliar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AuxToken",
                table: "Teams",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuxiliarName",
                table: "Teams",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_AuxToken",
                table: "Teams",
                column: "AuxToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Teams_AuxToken",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AuxToken",
                table: "Teams");

            migrationBuilder.DropColumn(
                name: "AuxiliarName",
                table: "Teams");
        }
    }
}
