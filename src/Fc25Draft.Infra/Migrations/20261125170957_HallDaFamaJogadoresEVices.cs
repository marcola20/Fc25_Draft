using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class HallDaFamaJogadoresEVices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TecnicoVice",
                table: "HallOfFame",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TimeVice",
                table: "HallOfFame",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TreinadorViceId",
                table: "HallOfFame",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HallDaFamaJogadores",
                columns: table => new
                {
                    HallDaFamaJogadorId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    Colocacao = table.Column<int>(type: "integer", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HallDaFamaJogadores", x => x.HallDaFamaJogadorId);
                    table.ForeignKey(
                        name: "FK_HallDaFamaJogadores_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HallOfFame_TreinadorViceId",
                table: "HallOfFame",
                column: "TreinadorViceId");

            migrationBuilder.CreateIndex(
                name: "IX_HallDaFamaJogadores_PlayerId",
                table: "HallDaFamaJogadores",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HallDaFamaJogadores_Temporada_Colocacao",
                table: "HallDaFamaJogadores",
                columns: new[] { "Temporada", "Colocacao" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HallDaFamaJogadores");

            migrationBuilder.DropIndex(
                name: "IX_HallOfFame_TreinadorViceId",
                table: "HallOfFame");

            migrationBuilder.DropColumn(
                name: "TecnicoVice",
                table: "HallOfFame");

            migrationBuilder.DropColumn(
                name: "TimeVice",
                table: "HallOfFame");

            migrationBuilder.DropColumn(
                name: "TreinadorViceId",
                table: "HallOfFame");
        }
    }
}
