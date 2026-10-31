using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fc25Draft.Infra.Migrations
{
    /// <inheritdoc />
    public partial class Padrinho : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AtributoDoPadrinho",
                table: "TreinosSemanais",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Apadrinhamentos",
                columns: table => new
                {
                    ApadrinhamentoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    Temporada = table.Column<int>(type: "integer", nullable: false),
                    PadrinhoId = table.Column<int>(type: "integer", nullable: false),
                    AfilhadoId = table.Column<int>(type: "integer", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apadrinhamentos", x => x.ApadrinhamentoId);
                    table.ForeignKey(
                        name: "FK_Apadrinhamentos_Players_AfilhadoId",
                        column: x => x.AfilhadoId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Apadrinhamentos_Players_PadrinhoId",
                        column: x => x.PadrinhoId,
                        principalTable: "Players",
                        principalColumn: "PlayerId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Apadrinhamentos_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "TeamId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Apadrinhamentos_AfilhadoId",
                table: "Apadrinhamentos",
                column: "AfilhadoId");

            migrationBuilder.CreateIndex(
                name: "IX_Apadrinhamentos_PadrinhoId",
                table: "Apadrinhamentos",
                column: "PadrinhoId");

            migrationBuilder.CreateIndex(
                name: "IX_Apadrinhamentos_TeamId_Temporada",
                table: "Apadrinhamentos",
                columns: new[] { "TeamId", "Temporada" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Apadrinhamentos");

            migrationBuilder.DropColumn(
                name: "AtributoDoPadrinho",
                table: "TreinosSemanais");
        }
    }
}
