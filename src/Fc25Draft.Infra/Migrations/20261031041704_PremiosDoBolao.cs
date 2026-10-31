using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class PremiosDoBolao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BolaoPremios",
                columns: table => new
                {
                    PremioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    RodadaId = table.Column<Guid>(type: "uuid", nullable: true),
                    TreinadorId = table.Column<Guid>(type: "uuid", nullable: true),
                    TimeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Posicao = table.Column<int>(type: "integer", nullable: false),
                    Pontos = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PagoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BolaoPremios", x => x.PremioId);
                    table.ForeignKey(
                        name: "FK_BolaoPremios_Teams_TimeId",
                        column: x => x.TimeId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BolaoPremios_Treinadores_TreinadorId",
                        column: x => x.TreinadorId,
                        principalTable: "Treinadores",
                        principalColumn: "TreinadorId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BolaoPremios_Chave",
                table: "BolaoPremios",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BolaoPremios_Temporada",
                table: "BolaoPremios",
                column: "Temporada");

            migrationBuilder.CreateIndex(
                name: "IX_BolaoPremios_TimeId",
                table: "BolaoPremios",
                column: "TimeId");

            migrationBuilder.CreateIndex(
                name: "IX_BolaoPremios_TreinadorId",
                table: "BolaoPremios",
                column: "TreinadorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BolaoPremios");
        }
    }
}
