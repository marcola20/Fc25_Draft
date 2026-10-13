using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class ApostasExtrasDoBolao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JogadorCasaId",
                table: "BolaoPalpites",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JogadorForaId",
                table: "BolaoPalpites",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BolaoPalpites_JogadorCasaId",
                table: "BolaoPalpites",
                column: "JogadorCasaId");

            migrationBuilder.CreateIndex(
                name: "IX_BolaoPalpites_JogadorForaId",
                table: "BolaoPalpites",
                column: "JogadorForaId");

            migrationBuilder.AddForeignKey(
                name: "FK_BolaoPalpites_Players_JogadorCasaId",
                table: "BolaoPalpites",
                column: "JogadorCasaId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BolaoPalpites_Players_JogadorForaId",
                table: "BolaoPalpites",
                column: "JogadorForaId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BolaoPalpites_Players_JogadorCasaId",
                table: "BolaoPalpites");

            migrationBuilder.DropForeignKey(
                name: "FK_BolaoPalpites_Players_JogadorForaId",
                table: "BolaoPalpites");

            migrationBuilder.DropIndex(
                name: "IX_BolaoPalpites_JogadorCasaId",
                table: "BolaoPalpites");

            migrationBuilder.DropIndex(
                name: "IX_BolaoPalpites_JogadorForaId",
                table: "BolaoPalpites");

            migrationBuilder.DropColumn(
                name: "JogadorCasaId",
                table: "BolaoPalpites");

            migrationBuilder.DropColumn(
                name: "JogadorForaId",
                table: "BolaoPalpites");
        }
    }
}
