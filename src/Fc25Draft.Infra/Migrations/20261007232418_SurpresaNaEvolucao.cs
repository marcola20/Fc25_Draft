using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class SurpresaNaEvolucao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Surpresa",
                table: "VariacoesDaTemporada",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Surpresa",
                table: "VariacoesDaTemporada");
        }
    }
}
