using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class AddEscalacoesPartidaESubstituicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "JogadorSaiuId",
                table: "LigaEventos",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LigaEscalacoesPartida",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    Titular = table.Column<bool>(type: "boolean", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaEscalacoesPartida", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LigaEscalacoesPartida_LigaPartidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "LigaPartidas",
                        principalColumn: "PartidaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaEscalacoesPartida_Players_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaEscalacoesPartida_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LigaEventos_JogadorSaiuId",
                table: "LigaEventos",
                column: "JogadorSaiuId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaEscalacoesPartida_JogadorId",
                table: "LigaEscalacoesPartida",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaEscalacoesPartida_PartidaId_TimeId_JogadorId",
                table: "LigaEscalacoesPartida",
                columns: new[] { "PartidaId", "TimeId", "JogadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LigaEscalacoesPartida_TimeId",
                table: "LigaEscalacoesPartida",
                column: "TimeId");

            migrationBuilder.AddForeignKey(
                name: "FK_LigaEventos_Players_JogadorSaiuId",
                table: "LigaEventos",
                column: "JogadorSaiuId",
                principalTable: "Players",
                principalColumn: "PlayerId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LigaEventos_Players_JogadorSaiuId",
                table: "LigaEventos");

            migrationBuilder.DropTable(
                name: "LigaEscalacoesPartida");

            migrationBuilder.DropIndex(
                name: "IX_LigaEventos_JogadorSaiuId",
                table: "LigaEventos");

            migrationBuilder.DropColumn(
                name: "JogadorSaiuId",
                table: "LigaEventos");
        }
    }
}
