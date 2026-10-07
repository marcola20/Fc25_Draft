using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PedidosDeDemissao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PedidosDemissao",
                columns: table => new
                {
                    PedidoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartidaOrigemId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartidaFalhaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pontos = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    DecididoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PedidosDemissao", x => x.PedidoId);
                    table.ForeignKey(
                        name: "FK_PedidosDemissao_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PedidosDemissao_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PedidosDemissao_Temporada",
                table: "PedidosDemissao",
                column: "Temporada");

            migrationBuilder.CreateIndex(
                name: "IX_PedidosDemissao_TimeId_PartidaFalhaId",
                table: "PedidosDemissao",
                columns: new[] { "TimeId", "PartidaFalhaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PedidosDemissao_TreinadorId",
                table: "PedidosDemissao",
                column: "TreinadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PedidosDemissao");
        }
    }
}
