using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class NotasDoPesPorJogador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LigaNotasJogadores",
                columns: table => new
                {
                    PartidaId = table.Column<Guid>(type: "uuid", nullable: false),
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nota = table.Column<decimal>(type: "numeric(3,1)", precision: 3, scale: 1, nullable: false),
                    MelhorEmCampo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaNotasJogadores", x => new { x.PartidaId, x.JogadorId });
                    table.ForeignKey(
                        name: "FK_LigaNotasJogadores_LigaPartidas_PartidaId",
                        column: x => x.PartidaId,
                        principalTable: "LigaPartidas",
                        principalColumn: "PartidaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaNotasJogadores_Players_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LigaNotasJogadores_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LigaNotasJogadores_JogadorId",
                table: "LigaNotasJogadores",
                column: "JogadorId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaNotasJogadores_TimeId",
                table: "LigaNotasJogadores",
                column: "TimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LigaNotasJogadores");
        }
    }
}
