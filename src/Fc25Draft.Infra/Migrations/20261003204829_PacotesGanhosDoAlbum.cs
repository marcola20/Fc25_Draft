using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PacotesGanhosDoAlbum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PontosBolaoPorPacote",
                table: "Albuns",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.CreateIndex(
                name: "IX_PacotesGanhos_Origem",
                table: "PacotesGanhos",
                column: "Origem");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PacotesGanhos_Origem",
                table: "PacotesGanhos");

            migrationBuilder.DropColumn(
                name: "PontosBolaoPorPacote",
                table: "Albuns");
        }
    }
}
