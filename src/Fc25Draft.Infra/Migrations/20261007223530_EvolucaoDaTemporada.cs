using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class EvolucaoDaTemporada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EvolucoesDaTemporada",
                columns: table => new
                {
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    AplicadaEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Jogadores = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EvolucoesDaTemporada", x => x.Temporada);
                });

            migrationBuilder.CreateTable(
                name: "VariacoesDaTemporada",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    Idade = table.Column<int>(type: "integer", nullable: false),
                    SemClube = table.Column<bool>(type: "boolean", nullable: false),
                    JogosDoClube = table.Column<int>(type: "integer", nullable: false),
                    Titular = table.Column<int>(type: "integer", nullable: false),
                    NotaMedia = table.Column<decimal>(type: "numeric(4,2)", nullable: true),
                    Curva = table.Column<int>(type: "integer", nullable: false),
                    Desempenho = table.Column<int>(type: "integer", nullable: false),
                    Variacao = table.Column<int>(type: "integer", nullable: false),
                    OverallAntes = table.Column<int>(type: "integer", nullable: false),
                    OverallDepois = table.Column<int>(type: "integer", nullable: false),
                    Explicacao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EvolucaoPesId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VariacoesDaTemporada", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VariacoesDaTemporada_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VariacoesDaTemporada_PlayerId",
                table: "VariacoesDaTemporada",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_VariacoesDaTemporada_Temporada_PlayerId",
                table: "VariacoesDaTemporada",
                columns: new[] { "Temporada", "PlayerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EvolucoesDaTemporada");

            migrationBuilder.DropTable(
                name: "VariacoesDaTemporada");
        }
    }
}
