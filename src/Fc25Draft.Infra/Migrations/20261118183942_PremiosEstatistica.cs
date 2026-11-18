using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PremiosEstatistica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PremiosEstatistica",
                columns: table => new
                {
                    PremioId = table.Column<Guid>(type: "uuid", nullable: false),
                    LigaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    Categoria = table.Column<int>(type: "integer", nullable: false),
                    Posicao = table.Column<int>(type: "integer", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PagoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Repasse = table.Column<bool>(type: "boolean", nullable: false),
                    OverParaPlayerId = table.Column<int>(type: "integer", nullable: true),
                    OverAplicadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    EvolucaoPesId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PremiosEstatistica", x => x.PremioId);
                    table.ForeignKey(
                        name: "FK_PremiosEstatistica_EvolucoesPes_EvolucaoPesId",
                        column: x => x.EvolucaoPesId,
                        principalTable: "EvolucoesPes",
                        principalColumn: "EvolucaoPesId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PremiosEstatistica_Ligas_LigaId",
                        column: x => x.LigaId,
                        principalTable: "Ligas",
                        principalColumn: "LigaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PremiosEstatistica_Players_OverParaPlayerId",
                        column: x => x.OverParaPlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PremiosEstatistica_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PremiosEstatistica_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PremiosEstatistica_EvolucaoPesId",
                table: "PremiosEstatistica",
                column: "EvolucaoPesId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosEstatistica_LigaId_Categoria_PlayerId",
                table: "PremiosEstatistica",
                columns: new[] { "LigaId", "Categoria", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PremiosEstatistica_OverParaPlayerId",
                table: "PremiosEstatistica",
                column: "OverParaPlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosEstatistica_PlayerId",
                table: "PremiosEstatistica",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosEstatistica_Temporada",
                table: "PremiosEstatistica",
                column: "Temporada");

            migrationBuilder.CreateIndex(
                name: "IX_PremiosEstatistica_TimeId",
                table: "PremiosEstatistica",
                column: "TimeId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PremiosEstatistica");
        }
    }
}
