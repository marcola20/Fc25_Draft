using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class JogosAnotados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LigaJogosAnotados",
                columns: table => new
                {
                    JogadorId = table.Column<int>(type: "integer", nullable: false),
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Jogos = table.Column<int>(type: "integer", nullable: false),
                    AtualizadoEmUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LigaJogosAnotados", x => new { x.JogadorId, x.LigaId, x.TimeId });
                    table.ForeignKey(
                        name: "FK_LigaJogosAnotados_Ligas_LigaId",
                        column: x => x.LigaId,
                        principalTable: "Ligas",
                        principalColumn: "LigaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaJogosAnotados_Players_JogadorId",
                        column: x => x.JogadorId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LigaJogosAnotados_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LigaJogosAnotados_LigaId",
                table: "LigaJogosAnotados",
                column: "LigaId");

            migrationBuilder.CreateIndex(
                name: "IX_LigaJogosAnotados_TimeId",
                table: "LigaJogosAnotados",
                column: "TimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LigaJogosAnotados");
        }
    }
}
